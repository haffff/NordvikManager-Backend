using Microsoft.EntityFrameworkCore;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Services
{
    /// <summary>
    /// How much may be uploaded into a game (Application Settings → Server, local admin): one
    /// pool for what the game's GMs (owner, co-GMs) uploaded, one per player for everyone else;
    /// 0 = no limit. Linked files (the host's own disk) and addon files don't count; uploads
    /// still arriving do (each counts from its start).
    /// </summary>
    public class UploadQuota
    {
        private const long MB = 1024 * 1024;
        /// <summary>An upload nothing arrived for in this long was given up (a reload starts it again): it no longer counts.</summary>
        public static readonly TimeSpan ActiveUpload = TimeSpan.FromMinutes(10);
        private readonly IServerSettingsStore settings;
        private readonly IFileStorageProvider storage;
        private readonly IUploadStore uploads;

        public UploadQuota(IServerSettingsStore settings, IFileStorageProvider storage, IUploadStore uploads)
        {
            this.settings = settings;
            this.storage = storage;
            this.uploads = uploads;
        }

        private sealed record Stored(Guid Id, Guid PlayerId, ResourceStorageKind Storage, string? Path, long BlobLength);

        /// <summary>
        /// Throws UploadLimitException when adding that many bytes would take the player (or
        /// the game's GMs) over the limit. replacing: the resource the bytes replace (only the
        /// growth counts); exceptUpload: the upload being finished (already counted as arriving).
        /// </summary>
        public async Task EnsureRoomAsync(IDbContext db, Guid gameId, Guid playerId, long adding,
            Guid? replacing = null, Guid? exceptUpload = null, CancellationToken cancellationToken = default)
        {
            if (adding <= 0)
                return;
            var limits = settings.Load().Uploads ?? new();
            var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
            if (game == null)
                return;
            var gms = new Dictionary<Guid, bool>();
            bool IsGm(Guid id) => gms.TryGetValue(id, out var gm) ? gm : gms[id] = game.IsRunBy(id);

            var asGm = IsGm(playerId);
            var limitMb = asGm ? limits.GmGameLimitMb : limits.PlayerLimitMb;
            if (limitMb <= 0)
                return;
            var limit = limitMb * MB;
            bool Counts(Guid owner) => asGm ? IsGm(owner) : owner == playerId;

            // the blobs' sizes are summed by the database: Data itself is never loaded
            var stored = await db.Resources
                .Where(r => r.GameId == gameId && r.Storage != ResourceStorageKind.Linked && EF.Property<Guid?>(r, "AddonModelId") == null)
                .Select(r => new Stored(r.Id, r.PlayerId, r.Storage, r.Path, r.Data == null ? 0 : (long)r.Data.Length))
                .ToListAsync(cancellationToken);
            long SizeOf(Stored r) => r.Storage == ResourceStorageKind.ManagedFile ? storage.GetStamp(r.Path ?? "")?.Length ?? 0 : r.BlobLength;

            if (replacing != null)
            {
                var old = stored.FirstOrDefault(r => r.Id == replacing);
                if (old != null && adding <= SizeOf(old))
                    return; // as big as before, or smaller: always fine
            }

            var used = stored.Where(r => r.Id != replacing && Counts(r.PlayerId)).Sum(SizeOf)
                + uploads.Unfinished(gameId, ActiveUpload).Where(u => u.ResourceId == null && !u.ForImport && u.Id != exceptUpload && Counts(u.PlayerId)).Sum(u => u.Size);
            if (used + adding > limit)
            {
                var whose = asGm ? "The game's GMs have" : "You have";
                throw new UploadLimitException(
                    $"That's over the server's upload limit: {whose} {Mb(used)} MB of {limitMb} MB in this game, and this file is {Mb(adding)} MB.");
            }
        }

        private static string Mb(long bytes) => Math.Ceiling(bytes / (double)MB).ToString("0");
    }
}

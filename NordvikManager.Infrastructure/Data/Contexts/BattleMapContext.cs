using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Chat;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NordvikManager.Data.Contexts
{
    public class DndOneContext : DbContext, IDbContext
    {
        public DndOneContext(DbContextOptions<DndOneContext> options) : base(options)
        {
            // The InMemory provider (unit tests — see HandlerTestBase) has no migrations
            // support at all, so it still needs EnsureCreated() to build its schema from
            // the current model. SQLite (the default/primary path) has a real migrations
            // set (see Migrations/) applied once at startup via Database.Migrate() (see
            // Startup.Configure) — never from here, since this is a scoped DI service
            // constructed fresh per request/WS message.
            //
            // MySQL deliberately still uses EnsureCreated() too: giving two relational
            // providers real migrations in one assembly needs a separate migrations
            // assembly/history table per provider (a single scan otherwise can't tell a
            // SQLite migration from a MySQL one for the same DbContext), and that setup
            // can't be verified here without a live MySQL server. Until someone actually
            // needs MySQL in production and can test it properly, this keeps its existing
            // behavior unchanged rather than risk a half-verified migration path.
            var providerName = Database.ProviderName ?? "";
            var hasNoMigrationsSupport =
                providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase);
            if (hasNoMigrationsSupport)
                Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Maps)
                .WithOne(m => m.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Players)
                .WithOne(p => p.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Layouts)
                .WithOne(l => l.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Resources)
                .WithOne(r => r.Game)
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.TreeEntries)
                .WithOne(t => t.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.BattleMaps)
                .WithOne(b => b.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Properties)
                .WithOne(p => p.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Cards)
                .WithOne(c => c.Game)
                .HasForeignKey(c => c.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            var cardKeyIndex = modelBuilder.Entity<CardModel>()
                .HasIndex(c => new { c.Key, c.GameId })
                .IsUnique();

            // SQLite allows multiple NULLs in unique indexes natively and does not support
            // filtered indexes. Apply the filter only on other providers (PostgreSQL, SQL Server).
            var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            if (!isSqlite)
                cardKeyIndex.HasFilter("\"Key\" IS NOT NULL");

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Actions)
                .WithOne(a => a.Game)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameModel>()
                .HasMany(g => g.Addons)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);

            // One copy of an addon per game: two installs at once can't both add it.
            modelBuilder.Entity<AddonModel>()
                .HasIndex("GameModelId", nameof(AddonModel.Key))
                .IsUnique();

            // The addons an addon needs, as JSON in one column (MySQL has no primitive collections).
            modelBuilder.Entity<AddonModel>()
                .Property(a => a.Dependencies)
                .HasConversion(
                    list => list == null || list.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(list, (System.Text.Json.JsonSerializerOptions?)null),
                    text => string.IsNullOrEmpty(text)
                        ? new List<AddonDependency>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<AddonDependency>>(text, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<AddonDependency>(),
                    new ValueComparer<List<AddonDependency>?>(
                        (a, b) => (a ?? new()).Select(d => d.Key + "@" + d.Version).SequenceEqual((b ?? new()).Select(d => d.Key + "@" + d.Version)),
                        list => (list ?? new()).Aggregate(0, (hash, d) => HashCode.Combine(hash, d.Key, d.Version)),
                        list => list == null ? null : list.Select(d => new AddonDependency { Key = d.Key, Version = d.Version }).ToList()));

            modelBuilder.Entity<MapModel>()
                .HasMany(m => m.Elements)
                .WithOne(e => e.Map)
                .HasForeignKey(e => e.MapId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MapModel>()
                .HasMany(m => m.Properties)
                .WithOne(p => p.Map)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ElementModel>()
                .HasMany(e => e.Properties)
                .WithOne(p => p.Element)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CardModel>()
                .HasMany(c => c.Properties)
                .WithOne(p => p.Card)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerModel>()
                .HasIndex(p => p.User)
                .IsUnique(false);

            modelBuilder.Entity<BattleMapModel>()
                .HasOne(b => b.Game)
                .WithMany(g => g.BattleMaps);

            modelBuilder.Entity<TreeEntryModel>()
                .HasOne(t => t.Parent);

            modelBuilder.Entity<TreeEntryModel>()
                .HasOne(t => t.Next);

            modelBuilder.Entity<PropertyModel>()
                .HasIndex(p => new { p.ParentID, p.Name })
                .IsUnique();

            modelBuilder.Entity<ResourceModel>()
                .HasOne(r => r.Game);

            modelBuilder.Entity<ResourceModel>()
                .HasOne(r => r.Player);

            modelBuilder.Entity<ResourceModel>()
                .HasIndex(r => new { r.Key, r.GameId })
                .IsUnique();

            modelBuilder.Entity<PermissionModel>()
                .HasIndex(p => p.ModelID)
                .IsUnique(false);

            modelBuilder.Entity<ElementDetailModel>().HasOne(e => e.Element).WithMany(e => e.Details).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ElementDetailModel>().HasIndex(e => new { e.ElementId, e.Key });

            modelBuilder.Entity<PlaylistModel>()
                .HasOne(p => p.Game)
                .WithMany(g => g.Playlists)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlaylistModel>()
                .HasMany(p => p.Resources)
                .WithMany(r => r.Playlists);

            // Kept as comma-separated ids in one column (MySQL has no primitive collections).
            modelBuilder.Entity<PlaylistModel>()
                .Property(p => p.LoopedResourceIds)
                .HasConversion(
                    ids => string.Join(',', ids),
                    text => text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList(),
                    new ValueComparer<List<Guid>>(
                        (a, b) => a!.SequenceEqual(b!),
                        ids => ids.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                        ids => ids.ToList()));

            // One turn order per map, gone with the map; entries gone with their order.
            // Entries point at their token by ElementId only (no FK): removing a token
            // removes its entry in RemoveElementCommandHandler, which also moves the turn on.
            modelBuilder.Entity<TurnOrderModel>()
                .HasOne(t => t.Map)
                .WithMany()
                .HasForeignKey(t => t.MapId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TurnOrderModel>().HasIndex(t => t.MapId).IsUnique();
            modelBuilder.Entity<TurnOrderEntryModel>()
                .HasOne(e => e.TurnOrder)
                .WithMany(t => t.Entries)
                .HasForeignKey(e => e.TurnOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public DbSet<GameModel>? Games { get; set; }
        public DbSet<MapModel>? Maps { get; set; }
        public DbSet<PlayerModel>? Players { get; set; }
        public DbSet<ElementModel>? Elements { get; set; }
        public DbSet<PropertyModel>? Properties { get; set; }
        public DbSet<PermissionModel>? Permissions { get; set; }
        public DbSet<ResourceModel>? Resources { get; set; }
        public DbSet<MessageModel>? Messages { get; set; }
        public DbSet<LayoutModel>? Layouts { get; set; }
        public DbSet<BattleMapModel>? BattleMaps { get; set; }
        public DbSet<CardModel>? Cards { get; set; }
        public DbSet<ActionModel>? Actions { get; set; }
        public DbSet<AddonModel>? Addons { get; set; }
        public DbSet<TreeEntryModel>? TreeEntries { get; set; }
        public DbSet<ElementDetailModel>? ElementsDetail { get; set; }
        public DbSet<BannedUserModel> BannedUsers { get; set; }
        public DbSet<PlaylistModel>? Playlists { get; set; }
        public DbSet<TurnOrderModel>? TurnOrders { get; set; }
        public DbSet<TurnOrderEntryModel>? TurnOrderEntries { get; set; }
        public DbSet<ServerSecretModel> ServerSecrets { get; set; }

        private sealed class BlobRow
        {
            public long RowId { get; set; }
            public long? Length { get; set; }
        }

        public async Task<byte[]?> ReadResourceSliceAsync(Guid resourceId, long start, int length, CancellationToken cancellationToken = default)
        {
            if (!Database.IsRelational())
                return null;
            var provider = Database.ProviderName ?? "";
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                return await ReadSqliteBlobSliceAsync(resourceId, start, length, cancellationToken);
            if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
            {
                // SQL positions count from 1. MySQL reads the whole value for SUBSTRING; database
                // blobs are kept small (big uploads go to file storage), so that stays bounded.
                var rows = await Database.SqlQuery<byte[]>($"SELECT SUBSTRING(`Data`, {start + 1}, {length}) AS `Value` FROM `Resources` WHERE `Id` = {resourceId}")
                    .ToListAsync(cancellationToken);
                return rows.Count == 0 ? null : rows[0] ?? Array.Empty<byte>();
            }
            return null;
        }

        // SQLite's incremental blob I/O: reads only the pages holding the part. (substr() would
        // load the whole value first.)
        private async Task<byte[]?> ReadSqliteBlobSliceAsync(Guid resourceId, long start, int length, CancellationToken cancellationToken)
        {
            await Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var rows = await Database.SqlQuery<BlobRow>($"SELECT rowid AS \"RowId\", length(\"Data\") AS \"Length\" FROM \"Resources\" WHERE \"Id\" = {resourceId}")
                    .ToListAsync(cancellationToken);
                if (rows.Count == 0)
                    return null;
                var total = rows[0].Length ?? 0;
                if (start >= total || length <= 0)
                    return Array.Empty<byte>();
                var buffer = new byte[(int)Math.Min(length, total - start)];
                using var blob = new Microsoft.Data.Sqlite.SqliteBlob((Microsoft.Data.Sqlite.SqliteConnection)Database.GetDbConnection(), "Resources", "Data", rows[0].RowId, readOnly: true);
                blob.Seek(start, SeekOrigin.Begin);
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = await blob.ReadAsync(buffer.AsMemory(read), cancellationToken);
                    if (n == 0) break;
                    read += n;
                }
                return read == buffer.Length ? buffer : buffer[..read];
            }
            finally
            {
                await Database.CloseConnectionAsync();
            }
        }
    }
}

using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Soundboard.GetSoundPlayback
{
    /// <summary>How a sound plays: how loud, and whether it repeats until stopped.</summary>
    public record SoundPlayback(double Volume, bool Loop);

    /// <summary>
    /// How the GM wants a sound played: the file's own volume, times its soundboard's when it
    /// was played from one, and looped when that soundboard loops it. Full volume, once, when
    /// nothing is set.
    /// </summary>
    public class GetSoundPlaybackCommand : CommandBase<SoundPlayback>
    {
        public Guid GameId { get; set; }
        public Guid ResourceId { get; set; }
        public Guid? SoundboardId { get; set; }
    }

    internal class GetSoundPlaybackCommandHandler : HandlerBase<GetSoundPlaybackCommand, SoundPlayback>
    {
        public GetSoundPlaybackCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<SoundPlayback> Handle(GetSoundPlaybackCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var fileVolume = await dbContext.Resources.AsNoTracking()
                .Where(r => r.Id == request.ResourceId && r.GameId == request.GameId)
                .Select(r => r.Volume)
                .FirstOrDefaultAsync(cancellationToken) ?? 1;

            var board = request.SoundboardId is Guid boardId
                ? await dbContext.Playlists.AsNoTracking()
                    .Where(p => p.Id == boardId && p.GameId == request.GameId)
                    .Select(p => new { p.Volume, p.LoopedResourceIds })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            return new SoundPlayback(
                VolumeRules.Clamp(fileVolume * (board?.Volume ?? 1)),
                board?.LoopedResourceIds.Contains(request.ResourceId) ?? false);
        }
    }
}

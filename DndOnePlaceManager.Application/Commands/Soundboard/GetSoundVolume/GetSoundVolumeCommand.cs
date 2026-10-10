using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Soundboard.GetSoundVolume
{
    /// <summary>
    /// How loud the GM wants a one-shot sound: the file's own volume, times its
    /// soundboard's when it was played from one. 1 when nothing is set.
    /// </summary>
    public class GetSoundVolumeCommand : CommandBase<double>
    {
        public Guid GameId { get; set; }
        public Guid ResourceId { get; set; }
        public Guid? SoundboardId { get; set; }
    }

    internal class GetSoundVolumeCommandHandler : HandlerBase<GetSoundVolumeCommand, double>
    {
        public GetSoundVolumeCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<double> Handle(GetSoundVolumeCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var fileVolume = await dbContext.Resources.AsNoTracking()
                .Where(r => r.Id == request.ResourceId && r.GameId == request.GameId)
                .Select(r => r.Volume)
                .FirstOrDefaultAsync(cancellationToken) ?? 1;

            var boardVolume = request.SoundboardId is Guid boardId
                ? await dbContext.Playlists.AsNoTracking()
                    .Where(p => p.Id == boardId && p.GameId == request.GameId)
                    .Select(p => (double?)p.Volume)
                    .FirstOrDefaultAsync(cancellationToken) ?? 1
                : 1;

            return VolumeRules.Clamp(fileVolume * boardVolume);
        }
    }
}

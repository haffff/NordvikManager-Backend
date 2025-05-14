
using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class GetResourceDataCommandHandler : HandlerBase<GetResourceDataCommand, (byte[], MimeType)>
    {
        public GetResourceDataCommandHandler(IMapper mapper , IDbContext ctx) : base(ctx, mapper)
        {
        }

        public async override Task<(byte[], MimeType)> Handle(GetResourceDataCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var image = await dbContext.Resources.FirstOrDefaultAsync(x => x.Id == request.ID || (x.Key != null && request.Key != null && x.Key == request.Key));
            var player = await dbContext.Players.FirstOrDefaultAsync(x => x.Id == request.Player.Id);

            //Todo - when getting from game. check for player

            //if (image != null && (image.PlayerId == request.Player.Id || player.System) && image.Game?.Id == request.GameID)
            if (image != null && image.Game?.Id == request.GameID)
            {
                return (image.Data, image.MimeType);
            }

            return (null,MimeType.None);
        }
    }
}


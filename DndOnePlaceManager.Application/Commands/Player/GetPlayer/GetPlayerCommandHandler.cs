
using AutoMapper;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class GetPlayerCommandHandler : HandlerBase<GetPlayerCommand, GetPlayerCommandResponse>
    {

        public GetPlayerCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<GetPlayerCommandResponse> Handle(GetPlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = (await dbContext.Games.Include(x => x.Players).FirstOrDefaultAsync(x => x.Id == request.GameID));
            var player = game?.Players?.FirstOrDefault(x => x.User == request.User.Id);
            if (player == null)
            {
                return new GetPlayerCommandResponse();
            }
            return new GetPlayerCommandResponse()
            {
                Player = new PlayerDTO()
                {
                    Name = player.Name,
                    Color = player.Color,
                    Image = player.Image,
                    Id = player.Id,
                    Permission = player.GetPermission(player.Id),
                    IsOwner = game?.MasterId == player.Id
                }
            };
        }
    }
}


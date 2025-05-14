
using AutoMapper;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class GetSystemPlayerCommandHandler : HandlerBase<GetSystemPlayerCommand, PlayerDTO>
    {
        public GetSystemPlayerCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<PlayerDTO> Handle(GetSystemPlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = (await dbContext.Games.Include(x => x.Players)?.FirstOrDefaultAsync(x => x.Id == request.GameID));
            var player = game?.Players?.FirstOrDefault(x => x.Id == game.SystemPlayerId);
            if (player == null)
            {
                return new PlayerDTO();
            }

            return new PlayerDTO()
            {
                Name = player.Name,
                Color = player.Color,
                Image = player.Image,
                Id = player.Id,
                Permission = player.GetPermission(player.Id),
                IsOwner = game?.MasterId == player.Id
            };
        }
    }
}


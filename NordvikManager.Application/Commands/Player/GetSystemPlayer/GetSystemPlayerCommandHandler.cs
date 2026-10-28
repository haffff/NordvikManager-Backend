
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.BattleMap
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
                Permission = game.GetPermission(player.Id),
                IsOwner = game?.MasterId == player.Id
            };
        }
    }
}


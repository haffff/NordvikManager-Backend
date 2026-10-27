
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.BattleMap
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
            var player = game?.Players?.FirstOrDefault(x =>
                x.CentralServerUserId == request.User.Id);
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
                    // The player's permission on the game (falls back to the "everyone" row).
                    Permission = game.GetPermission(player.Id),
                    IsOwner = game?.MasterId == player.Id,
                    CentralServerUserId = player.CentralServerUserId
                }
            };
        }
    }
}


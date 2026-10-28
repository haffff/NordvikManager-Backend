using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Layouts.AddLayout
{
    internal class AddLayoutCommandHandler : GenericAddHandler<AddLayoutCommand, LayoutModel, LayoutDTO>
    {
        public AddLayoutCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override bool CheckPermissions(GameModel game, AddLayoutCommand request)
        {
            var playerId = request.Player.Id ?? Guid.Empty;

            // The GM (or anyone with game-Edit) may always create layouts.
            if (game.MasterId == playerId || game.HasPermission(playerId, Domain.Enums.Permission.Edit))
                return true;

            // Otherwise honour the per-game "disallowPlayerLayouts" toggle (absent => allowed).
            var disallow = game.Properties?.FirstOrDefault(p => p.Name == "disallowPlayerLayouts")?.Value;
            if (string.Equals(disallow, "true", StringComparison.OrdinalIgnoreCase))
                throw new PermissionException(Domain.Enums.Permission.Edit);

            return true;
        }

        public override GameModel GetGame(AddLayoutCommand request)
        {
            // Only the setting CheckPermissions reads — not every layout's JSON or every property.
            return dbContext.Games
                .Include(x => x.Properties.Where(p => p.Name == "disallowPlayerLayouts"))
                .FirstOrDefault(x => x.Id == request.GameID);
        }

        public override LayoutModel CreateModel(GameModel game, AddLayoutCommand request)
        {
            var model = base.CreateModel(game, request);

            model.Id = default;
            model.GameModelId = request.GameID;
            model.Game = game;

            // Only one layout per game may be the default — clear the flag on the others
            // (GenericAddHandler saves once, so this persists in the same transaction).
            if (request.Dto.Default == true)
            {
                foreach (var other in dbContext.Layouts.Where(x => x.GameModelId == game.Id && x.Default).ToList())
                    other.Default = false;
            }

            return model;
        }

        public override void SetPermissions(GameModel game, LayoutModel model, AddLayoutCommand request)
        {
            model.SetPermissions(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.All);
            model.SetPermissions(game.SystemPlayerId, Domain.Enums.Permission.All);
        }
    }
}

using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Layouts.AddLayout
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
            return dbContext.Games
                .Include(x => x.Layouts)
                .Include(x => x.Properties)
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
                foreach (var other in game.Layouts.Where(x => x.Default))
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

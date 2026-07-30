using AutoMapper;
using DndOnePlaceManager.Application.Commands.Game.DeleteGame;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Game.RemoveGame
{
    internal class RemoveGameCommandHandler : HandlerBase<RemoveGameCommand, CommandResponse>
    {
        public RemoveGameCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<CommandResponse> Handle(RemoveGameCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games
                .Include(x => x.Addons).ThenInclude(a => a.Views)
                .Include(x => x.Addons).ThenInclude(a => a.Templates)
                .Include(x => x.Addons).ThenInclude(a => a.Actions)
                .Include(x => x.Addons).ThenInclude(a => a.Resources)
                .FirstOrDefault(x => x.Id == request.GameID);

            Guard.NotFound(game, "Game", request.GameID);

            game.ThrowIfNoPermission(request.Player?.Id ?? Guid.Empty, Domain.Enums.Permission.Remove);

            //Get ids to clear up permissions
            var players = game.Players.Select(x => x.Id);

            var allPermissions = dbContext.Permissions.Where(x => players.Contains(x.PlayerID));
            dbContext.Permissions.RemoveRange(allPermissions);

            dbContext.Games.Remove(game);

            dbContext.SaveChanges();
            return CommandResponse.Ok;
        }
    }
}

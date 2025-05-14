using AutoMapper;
using DndOnePlaceManager.Application.Commands.Game.DeleteGame;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
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
                //.Include(x=>x.Actions)
                //.Include(x => x.Players)
                //.Include(x => x.BattleMaps)
                //.Include(x=>x.Layouts)
                //.Include(x=>x.Maps).ThenInclude(x=>x.Elements).ThenInclude(x=>x.Properties)
                //.Include(x=>x.Maps).ThenInclude(x=>x.Properties)
                //.Include(x=>x.Cards).ThenInclude(x=>x.Properties)
                //.Include(x => x.Resources)
                //.Include(x=>x.Addons)
                //.Include(x=>x.TreeEntries)
                //.Include(x => x.Properties)
                .FirstOrDefault(x => x.Id == request.GameID);

            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

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

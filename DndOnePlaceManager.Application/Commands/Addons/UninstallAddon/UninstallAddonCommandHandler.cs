using AutoMapper;
using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Card;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Addons.UninstallAddon
{
    internal class UninstallAddonCommandHandler : HandlerBase<UninstallAddonCommand, CommandResponse>
    {
        private readonly IMediator mediator;

        public UninstallAddonCommandHandler(IDbContext dbContext, IMediator mediator, IMapper mapper) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(UninstallAddonCommand request, CancellationToken token)
        {
            //Get game for futher processing
            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameID);

            if (!game.HasPermission(request.Player.Id ?? default, Permission.Edit))
            {
                return CommandResponse.NoPermission;
            }

            var addon = dbContext.Addons
                .Include(x => x.Views)
                .Include(x => x.Resources)
                .Include(x => x.Templates)
                .Include(x => x.Actions)
                .FirstOrDefault(x => x.Id == request.AddonId);

            if (addon == null)
            {
                return CommandResponse.NoResource;
            }

            //Remove all resources
            foreach (var resource in addon.Resources.ToList())
            {
                RemoveResourceCommand removeResourceCommand = new RemoveResourceCommand()
                {
                    Player = request.Player,
                    ID = resource.Id,
                    GameId = request.GameID
                };

                await mediator.Send(removeResourceCommand);
            }

            //Remove all views
            foreach (var view in addon.Views.ToList())
            {
                RemoveCardCommand removeCardCommand = new RemoveCardCommand()
                {
                    Player = request.Player,
                    Id = view.Id,
                    GameID = request.GameID
                };

                await mediator.Send(removeCardCommand);
            }

            //Remove all templates
            foreach (var template in addon.Templates.ToList())
            {
                RemoveCardCommand removeCardCommand = new RemoveCardCommand()
                {
                    Player = request.Player,
                    Id = template.Id,
                    GameID = request.GameID
                };

                await mediator.Send(removeCardCommand);
            }

            //Remove all actions
            foreach (var action in addon.Actions.ToList())
            {
                RemoveActionCommand removeActionCommand = new RemoveActionCommand()
                {
                    Player = request.Player,
                    Id = action.Id,
                    GameID = request.GameID
                };

                await mediator.Send(removeActionCommand);
            }

            dbContext.Remove(addon);
            await dbContext.SaveChangesAsync();
            return CommandResponse.Ok;
        }
    }
}

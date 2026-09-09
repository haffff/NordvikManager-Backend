using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace DndOnePlaceManager.Application.Commands.Layouts.ForceLayout
{
    // Does NO DB write. Returns Ok purely so GameLobby keeps the command and broadcasts
    // the layout_forcechange frame to every connected player; each client then applies
    // the layout locally via its existing "layout_forcechange" subscription.
    public class ForceLayoutCommandHandler : HandlerBase<ForceLayoutCommand, CommandResponse>
    {
        public ForceLayoutCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(ForceLayoutCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games.Include(x => x.Layouts).FirstOrDefault(x => x.Id == request.GameID);
            Guard.NotFound(game, "Game", request.GameID);

            game.ThrowIfNoPermission(request.Player?.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            Guard.NotFound(game.Layouts.FirstOrDefault(x => x.Id == request.LayoutId), "Layout", request.LayoutId);

            return CommandResponse.Ok;
        }
    }
}

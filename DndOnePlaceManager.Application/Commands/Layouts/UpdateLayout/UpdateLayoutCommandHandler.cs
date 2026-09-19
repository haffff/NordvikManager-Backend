using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;

namespace DndOnePlaceManager.Application.Commands.Layouts.UpdateLayout
{
    public class UpdateLayoutCommandHandler : HandlerBase<UpdateLayoutCommand, CommandResponse>
    {
        public UpdateLayoutCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }


        public override async Task<CommandResponse> Handle(UpdateLayoutCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = dbContext.Games.Include(x => x.Layouts).FirstOrDefault(x => x.Id == request.Dto.GameModelId);

            Guard.Argument(game != null && request.Player != null, nameof(request.Dto));

            var layout = dbContext.Layouts.FirstOrDefault(x => x.Id == request.Dto.Id);

            Guard.NotFound(layout, "Layout", request.Dto.Id);

            layout.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            // Only one layout per game may be the default — clear the flag on the others.
            if (request.Dto.Default == true && !layout.Default)
            {
                foreach (var other in game.Layouts.Where(x => x.Id != layout.Id && x.Default))
                    other.Default = false;
            }

            layout.Default = request.Dto.Default ?? layout.Default;
            layout.Value = request.Dto.Value ?? layout.Value;
            layout.Name = request.Dto.Name ?? layout.Name;

            return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;
        }
    }
}

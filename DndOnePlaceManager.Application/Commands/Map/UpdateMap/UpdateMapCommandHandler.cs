using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;

namespace DndOnePlaceManager.Application.Commands.Map.UpdateMap
{
    internal class UpdateMapCommandHandler : HandlerBase<UpdateMapCommand, CommandResponse>
    {
        public UpdateMapCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdateMapCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (!dbContext.Games.Any(x => x.Id == request.GameId))
                throw new ResourceNotFoundException("Game", request.GameId);

            var map = dbContext.Maps.FirstOrDefault(x => x.Id == request.Map.Id && x.Game.Id == request.GameId);

            Guard.NotFound(map, "Map", request.Map.Id);

            map.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            map.Name = request?.Map.Name ?? map.Name;
            map.GridVisible = request?.Map.GridVisible ?? map.GridVisible;
            map.GridSize = request?.Map.GridSize ?? map.GridSize;
            map.GridColor = request?.Map.GridColor ?? map.GridColor;
            map.Width = request?.Map.Width ?? map.Width;
            map.Height = request?.Map.Height ?? map.Height;

            return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;
        }
    }
}

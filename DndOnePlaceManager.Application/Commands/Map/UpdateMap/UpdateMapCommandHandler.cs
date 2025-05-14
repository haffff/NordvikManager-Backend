using AutoMapper;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
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
            var game = dbContext.Games.Include(x => x.Maps).FirstOrDefault(x => x.Id == request.GameId);
            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

            var map = game.Maps.FirstOrDefault(x => x.Id == request.Map.Id);

            if (map == null)
            {
                throw new ResourceNotFoundException(nameof(map));
            }

            map.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            map.Name = request?.Map.Name ?? map.Name;
            map.GridVisible = request?.Map.GridVisible ?? map.GridVisible;
            map.GridSize = request?.Map.GridSize ?? map.GridSize;
            map.Width = request?.Map.Width ?? map.Width;
            map.Height = request?.Map.Height ?? map.Height;

            return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;
        }
    }
}

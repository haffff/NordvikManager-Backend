using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
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

            if (game == null || request.Player == null)
            {
                throw new WrongArgumentsException(nameof(request.Dto));
            }

            var layout = dbContext.Layouts.FirstOrDefault(x => x.Id == request.Dto.Id);

            if(layout == null)
            {
                throw new ResourceNotFoundException(nameof(layout));
            }

            layout.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            layout.Default = request.Dto.Default ?? layout.Default;
            layout.Value = request.Dto.Value ?? layout.Value;
            layout.Name = request.Dto.Name ?? layout.Name;

            return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;
        }
    }
}

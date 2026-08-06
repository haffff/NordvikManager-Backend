
using AutoMapper;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Commands.Elements
{
    internal class DeleteElementCommandHandler : GenericDeleteHandler<DeleteElementCommand, ElementModel>
    {

        public DeleteElementCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public override Task<CommandResponse> Handle(DeleteElementCommand request, CancellationToken cancellationToken)
        {
            dbContext.Properties.Where(x => x.ParentID == request.Id).ToList().ForEach(x => dbContext.Remove(x));
            dbContext.SaveChanges();
            return base.Handle(request, cancellationToken);

        }
    }
}


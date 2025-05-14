
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

        //public override async Task<CommandResponse> Handle(DeleteElementCommand request, CancellationToken cancellationToken)
        //{
        //    await base.Handle(request, cancellationToken);
        //    var element = dbContext.Find<ElementModel>(request.Id);
        //    if (dbContext.Find<MapModel>(element.MapId).HasPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit) &&
        //        element.HasPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Remove))
        //    {
        //        var permissions = dbContext.Permissions.Where(x => x.ModelID == request.Id);
        //        dbContext.RemoveRange(permissions);
        //        var properties = dbContext.Properties.Where(x => x.ParentID == request.Id);
        //        dbContext.RemoveRange(properties);
        //        dbContext.Remove(dbContext.Find<ElementModel>(request.Id));
        //        return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.WrongArguments;
        //    }
        //    return CommandResponse.NoPermission;
        //}
    }
}


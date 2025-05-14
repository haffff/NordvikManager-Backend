using AutoMapper;
using DndOnePlaceManager.Application.Commands;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Generic.Handlers
{
    internal class GenericGetListHandler<TCommand, TModel, TResponse> : HandlerBase<TCommand, List<TResponse>>
        where TCommand : GenericGetListCommand<TResponse>
        where TResponse : IGameDataTransferObject
        where TModel : class, IEntity
    {
        public GenericGetListHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public virtual IEnumerable<TModel> GetEntities(TCommand request)
        {
            throw new NotImplementedException();
        }

        public virtual List<TModel> FilterOutByPermission(List<TModel> models, TModel entity, Guid playerId)
        {
            return models.WithPermission(playerId).ToList();
        }

        public virtual List<TResponse> ModifyOutput(List<TResponse> response)
        {
            return response;
        }

        public override async Task<List<TResponse>> Handle(TCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var entities = GetEntities(request);

            if (entities == null)
            {
                return default(List<TResponse>);
            }

            var filteredEntities = FilterOutByPermission(entities.ToList(), entities.FirstOrDefault(), request.Player.Id ?? Guid.Empty);

            var mappedDTOs = mapper.Map<IEnumerable<TResponse>>(entities).ToList();
            var modifiedDTOs = ModifyOutput(mappedDTOs);
            
            return modifiedDTOs;
        }
    }
}

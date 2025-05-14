using AutoMapper;
using DndOnePlaceManager.Application.Commands;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Generic.Handlers
{
    internal class GenericGetHandler<TCommand, TModel, TResponse> : HandlerBase<TCommand,TResponse> 
        where TCommand : GenericGetCommand<TResponse> 
        where TResponse : IGameDataTransferObject 
        where TModel : class, IEntity
    {
        public GenericGetHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public virtual TModel GetEntity(TCommand request)
        {
            return dbContext.Find(typeof(TModel), request.Id) as TModel;
        }

        public virtual void GetPermissions(TResponse dto ,TModel entity, Guid playerId)
        {
            dto.Permission = entity.GetPermission(playerId);
        }

        public virtual TResponse ModifyOutput(TResponse response)
        {
            return response;
        }

        public override async Task<TResponse> Handle(TCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var entity = GetEntity(request);

            if(entity == null)
            {
                return default(TResponse);
            }

            if(entity.HasPermission(request.Player.Id ?? Guid.Empty))
            {
                var mappedDTO = mapper.Map<TResponse>(entity);

                GetPermissions(mappedDTO, entity, request.Player.Id ?? Guid.Empty);

                mappedDTO.Permission = entity.GetPermission(request.Player.Id ?? Guid.Empty);

                mappedDTO = ModifyOutput(mappedDTO);

                return mappedDTO;
            }

            return default(TResponse);
        }
    }
}

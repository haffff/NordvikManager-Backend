using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Generic.Handlers
{
    internal class GenericDeleteHandler<TCommand, TModel> : HandlerBase<TCommand, CommandResponse>
        where TCommand : GenericDeleteCommand
        where TModel : class, IEntity
    {
        public GenericDeleteHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public virtual bool CheckPermissions(TCommand request, TModel model)
        {
            model.ThrowIfNoPermission(request.Player.Id ?? System.Guid.Empty, Permission.Remove);
            return true;
        }

        public override async Task<CommandResponse> Handle(TCommand request, CancellationToken cancellationToken)
        {
            var model = dbContext.Find(typeof(TModel), request.Id) as TModel;

            if (model == null)
            {
                throw new ResourceNotFoundException(nameof(model));
            }

            if (!CheckPermissions(request, model))
            {
                throw new PermissionException(Permission.Remove);
            }

            var properties = dbContext.Properties.Where(x => x.ParentID == model.Id);
            dbContext.RemoveRange(properties);

            var permissions = dbContext.Permissions.Where(x => x.ModelID == model.Id);
            dbContext.RemoveRange(permissions);

            dbContext.Remove(model);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}

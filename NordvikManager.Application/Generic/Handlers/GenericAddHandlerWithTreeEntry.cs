using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;

namespace NordvikManager.Application.Generic.Handlers
{
    internal class GenericAddHandlerWithTreeEntry<TCommand, TModel, TDto> : GenericAddHandler<TCommand, TModel, TDto>
        where TCommand : GenericAddCommand<TDto>
        where TModel : class, INamedEntity
        where TDto : class, IGameDataTransferObject
    {
        private readonly IMediator mediator;
        /// <summary>
        /// Folder tree the new entity goes into (see TreeEntryTypes); defaults to the model's
        /// name. Handlers whose model is shown in several panels set it per request.
        /// </summary>
        protected string? TreeEntryType { get; set; }

        public GenericAddHandlerWithTreeEntry(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, Guid)> Handle(TCommand request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken);

            if (result.Item1 != CommandResponse.Ok)
            {
                return result;
            }

            var model = dbContext.Find<TModel>(result.Item2);

            TreeEntryDto treeEntry = new TreeEntryDto
            {
                Name = model.Name,
                EntryType = TreeEntryType ?? typeof(TModel).Name,
                IsFolder = false,
                TargetId = result.Item2,
                ParentId = request.TreeParentId
            };

            await mediator.Send(new AddTreeEntryCommand()
            {
                TreeEntryDto = treeEntry,
                GameId = request.GameID,
                Player = request.Player
            });

            return result;
        }
    }
}

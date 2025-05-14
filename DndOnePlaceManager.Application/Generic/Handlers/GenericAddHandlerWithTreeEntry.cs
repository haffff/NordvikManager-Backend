using AutoMapper;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;

namespace DndOnePlaceManager.Application.Generic.Handlers
{
    internal class GenericAddHandlerWithTreeEntry<TCommand, TModel, TDto> : GenericAddHandler<TCommand, TModel, TDto>
        where TCommand : GenericAddCommand<TDto>
        where TModel : class, INamedEntity
        where TDto : class, IGameDataTransferObject
    {
        private readonly IMediator mediator;
        protected bool OmitTreeCreation { get; set; } = false;

        public GenericAddHandlerWithTreeEntry(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, Guid)> Handle(TCommand request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken);

            if(result.Item1 != CommandResponse.Ok || OmitTreeCreation)
            {
                return result;
            }

            var model = dbContext.Find<TModel>(result.Item2);

            TreeEntryDto treeEntry = new TreeEntryDto
            {
                Name = model.Name,
                EntryType = typeof(TModel).Name,
                IsFolder = false,
                TargetId = result.Item2
            };

            var (newResult, affectedDtos) = await mediator.Send(new AddTreeEntryCommand()
            {
                TreeEntryDto = treeEntry,
                GameId = request.GameID,
                Player = request.Player
            });

            if(newResult == CommandResponse.Ok)
            {
                return result;
            }

            return (CommandResponse.WrongArguments, default);
        }
    }
}

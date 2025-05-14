using AutoMapper;
using DndOnePlaceManager.Application.Commands.TreeEntry.ConnectTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries
{
    internal class GetTreeEntriesCommandHandler : HandlerBase<GetTreeEntriesCommand,List<TreeEntryDto>>
    {
        private readonly IMediator mediator;

        public GetTreeEntriesCommandHandler(IDbContext ctx, IMapper mapper, IMediator mediator) : base(ctx, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<List<TreeEntryDto>> Handle(GetTreeEntriesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            ConnectTreeEntriesCommand connectTreeEntriesCommand = new ConnectTreeEntriesCommand()
            {
                GameID = request.GameId ?? default,
                EntityType = request.EntityType
            };

            await mediator.Send(connectTreeEntriesCommand, cancellationToken);

            var treeEntries = dbContext.Games
                .Include(x=>x.TreeEntries).ThenInclude(x => x.Parent)
                .Include(x => x.TreeEntries).ThenInclude(x => x.Next)
                .FirstOrDefault(x => request.GameId == x.Id)
                .TreeEntries
                .Where(x => x.EntryType == request.EntityType)
                .ToList();

            return mapper.Map<List<TreeEntryDto>>(treeEntries);
        }
    }
}

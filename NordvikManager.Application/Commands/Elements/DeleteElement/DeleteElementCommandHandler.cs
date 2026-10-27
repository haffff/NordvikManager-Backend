
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.TurnOrder;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Application.Commands.Elements
{
    internal class DeleteElementCommandHandler : GenericDeleteHandler<DeleteElementCommand, ElementModel>
    {

        public DeleteElementCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(DeleteElementCommand request, CancellationToken cancellationToken)
        {
            dbContext.Properties.Where(x => x.ParentID == request.Id).ToList().ForEach(x => dbContext.Remove(x));
            dbContext.SaveChanges();
            var response = await base.Handle(request, cancellationToken);

            // A removed token leaves its map's turn order (passing the turn on if it was its).
            // Clients refetch the order on element_remove.
            if (response == CommandResponse.Ok)
                await TurnOrderCleanup.RemoveElementAsync(dbContext, request.Id);
            return response;
        }
    }
}


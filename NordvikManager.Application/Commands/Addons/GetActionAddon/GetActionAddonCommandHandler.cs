using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Addons.GetActionAddon
{
    internal class GetActionAddonCommandHandler : HandlerBase<GetActionAddonCommand, string?>
    {
        public GetActionAddonCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<string?> Handle(GetActionAddonCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // One query, the key only (an addon's actions are its Actions relation).
            return await dbContext.Addons
                .Where(a => a.Actions!.Any(x => x.Id == request.ActionId))
                .Select(a => a.Key)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}

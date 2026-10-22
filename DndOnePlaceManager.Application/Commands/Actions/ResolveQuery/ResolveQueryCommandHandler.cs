using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Services.Actions;
using DndOnePlaceManager.Infrastructure.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Actions.ResolveQuery
{
    /// <summary>
    /// The action editor's Query Resolver: fills in an expression's tokens exactly as a
    /// running action does (<see cref="ActionTokenResolver"/>), with gameId and playerId set.
    /// </summary>
    internal class ResolveQueryCommandHandler : HandlerBase<ResolveQueryCommand, ResolveQueryResult>
    {
        public ResolveQueryCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper) { }

        public override async Task<ResolveQueryResult> Handle(ResolveQueryCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var vars = request.Variables ?? new Dictionary<string, object>();
            vars["gameId"] = request.GameId.ToString();
            if (request.Player?.Id.HasValue == true)
                vars["playerId"] = request.Player.Id.Value.ToString();

            var resolver = new ActionTokenResolver(dbContext, request.GameId);
            var result = await resolver.ResolveAsync(request.Expression, vars);
            return new ResolveQueryResult { Result = result, Unresolved = resolver.Unresolved };
        }
    }
}

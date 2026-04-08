using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.GetLocalPlayers
{
    internal class GetLocalPlayersCommandHandler : HandlerBase<GetLocalPlayersCommand, GetLocalPlayersCommandResponse>
    {
        public GetLocalPlayersCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<GetLocalPlayersCommandResponse> Handle(GetLocalPlayersCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var all = await dbContext.Players
                .Where(p => !p.System && (p.User != null || p.CentralServerUserId != null))
                .ToListAsync(cancellationToken);

            var total = all.Count;
            var paged = all
                .Skip((request.Page - 1) * request.Count)
                .Take(request.Count)
                .Select(p => new LocalPlayerDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    CentralServerUserId = p.CentralServerUserId ?? p.User,
                    Color = p.Color
                })
                .ToList();

            return new GetLocalPlayersCommandResponse
            {
                Page = request.Page,
                Count = request.Count,
                Total = total,
                Data = paged
            };
        }
    }
}

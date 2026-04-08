using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Game.GetGameSessionDetails
{
    internal class GetGameSessionDetailsCommandHandler : HandlerBase<GetGameSessionDetailsCommand, GetGameSessionDetailsResult?>
    {
        public GetGameSessionDetailsCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<GetGameSessionDetailsResult?> Handle(GetGameSessionDetailsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = await dbContext.Games
                .Include(g => g.Properties)
                .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            if (game == null)
                return null;

            return new GetGameSessionDetailsResult
            {
                Name = game.Name ?? string.Empty,
                Summary = game.Properties?.FirstOrDefault(p => p.Name == "shortDescription")?.Value,
                Description = game.Properties?.FirstOrDefault(p => p.Name == "longDescription")?.Value,
                PasswordRequired = game.Password != null,
                IsPublic = game.IsPublic
            };
        }
    }
}


using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class GetGameListCommandHandler : HandlerBase<GetGameListCommand, GetGameListCommandResponse>
    {
        public GetGameListCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<GetGameListCommandResponse> Handle(GetGameListCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var result = await dbContext.Games
                .Include(x => x.Players)
                .Include(x => x.Properties)
                .Where(x => x.Players.Any(y => y.User == request.UserId)).ToListAsync();

            var mapped = result.Select( x =>
            {
                string? longDescription = x.Properties.FirstOrDefault(y => y.Name == "longDescription")?.Value;
                string? shortDescription = x.Properties.FirstOrDefault(y => y.Name == "shortDescription")?.Value;
                string? color = x.Properties.FirstOrDefault(y => y.Name == "color")?.Value;
                string? image = x.Properties.FirstOrDefault(y => y.Name == "image")?.Value;

                GameItemDTO gameItem = new GameItemDTO()
                {
                    Id = x.Id,
                    Name = x.Name,
                    LongDescription = longDescription,
                    ShortDescription = shortDescription,
                    Image = image,
                    Color = color,
                    IsOwner = x.Players.FirstOrDefault(y => y.User == request.UserId)?.Id == x.MasterId
                };

                return gameItem;
            });


            return new GetGameListCommandResponse() { GameItemList = mapped.ToList() };
        }
    }
}


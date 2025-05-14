using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class AddPlayerCommandHandler : HandlerBase<AddPlayerCommand, Guid?>
    {
        public AddPlayerCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<Guid?> Handle(AddPlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = (await dbContext.Games.Include(x => x.Players)?.FirstOrDefaultAsync(x => x.Id == request.GameID));

            if (game == null || (game.Password != request.Password && !string.IsNullOrWhiteSpace(game.Password)))
            {
                return null;
            }

            var player = game?.Players?.FirstOrDefault(x => x.User == request.User?.Id);

            if (player == null)
            {
                var random = new Random();

                var red = random.Next(0, 255 / 10) * 10;
                var green = random.Next(0, 255 / 10) * 10;
                var blue = random.Next(0, 255 / 10) * 10;

                var newPlayer = new PlayerModel() 
                { 
                    Name = "Player", 
                    User = request.User?.Id, 
                    Color = $"rgba({red},{green},{blue},1)",
                    Image = string.Empty
                };
                var result = dbContext.Players.Add(newPlayer);

                game.Players.Add(newPlayer);

                await dbContext.SaveChangesAsync();

                return newPlayer.Id;
            }
            else
            {
                return player.Id;
            }
        }
    }
}


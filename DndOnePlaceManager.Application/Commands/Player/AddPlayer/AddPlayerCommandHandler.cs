using AutoMapper;
using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class AddPlayerCommandHandler : HandlerBase<AddPlayerCommand, Guid?>
    {
        private readonly IMediator mediator;
        private readonly ILogger<AddPlayerCommandHandler> logger;

        public AddPlayerCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator, ILogger<AddPlayerCommandHandler> logger) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        public async override Task<Guid?> Handle(AddPlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = (await dbContext.Games.Include(x => x.Players)?.FirstOrDefaultAsync(x => x.Id == request.GameID));

            if (game == null)
            {
                return null;
            }

            if (!request.SkipPasswordCheck && game.Password != request.Password && !string.IsNullOrWhiteSpace(game.Password))
            {
                return null;
            }

            var player = game?.Players?.FirstOrDefault(x => x.CentralServerUserId == request.User?.Id);

            if (player == null)
            {
                var random = new Random();

                var red = random.Next(0, 255 / 10) * 10;
                var green = random.Next(0, 255 / 10) * 10;
                var blue = random.Next(0, 255 / 10) * 10;

                var newPlayer = new PlayerModel()
                {
                    Name = "Player",
                    CentralServerUserId = request.User?.Id,
                    Color = $"rgba({red},{green},{blue},1)",
                    Image = string.Empty
                };
                var result = dbContext.Players.Add(newPlayer);

                game.Players.Add(newPlayer);

                await dbContext.SaveChangesAsync();

                await CreateDefaultCharacterSheetAsync(game, newPlayer, cancellationToken);

                return newPlayer.Id;
            }
            else
            {
                return player.Id;
            }
        }

        // "Add default Character sheet to player" / "Character sheet template" (GameSettingsPanel,
        // Character Sheets tab) are stored as GameModel properties, not GameModel columns — read
        // them the same way GetPropertyValueStepDefinition does. Best-effort: a missing/invalid
        // template must never block the player from actually joining the game.
        private async Task CreateDefaultCharacterSheetAsync(GameModel game, PlayerModel newPlayer, CancellationToken cancellationToken)
        {
            try
            {
                var settingsProps = await dbContext.Properties
                    .Where(x => x.ParentID == game.Id
                        && (x.Name == "useDefaultCharacterSheets" || x.Name == "characterSheetTemplate"))
                    .ToListAsync(cancellationToken);

                var useDefaultRaw = settingsProps.FirstOrDefault(x => x.Name == "useDefaultCharacterSheets")?.Value;
                var templateIdRaw = settingsProps.FirstOrDefault(x => x.Name == "characterSheetTemplate")?.Value;

                if (!bool.TryParse(useDefaultRaw, out var useDefault) || !useDefault)
                    return;

                if (!Guid.TryParse(templateIdRaw, out var templateId))
                    return;

                // AddCardCommandHandler requires its acting Player to have Edit on the
                // game (GenericAddHandler.CheckPermissions) — a brand-new player never
                // does. Act as the GM here; the new player still gets Edit+Read on their
                // own sheet via the Dto.Owner branch in AddCardCommandHandler.SetPermissions.
                var gm = game.Players?.FirstOrDefault(x => x.Id == game.MasterId);
                if (gm == null)
                    return;

                var creator = mapper.Map<PlayerDTO>(gm);

                await mediator.Send(new AddCardCommand
                {
                    Dto = new CardDto
                    {
                        Name = $"{newPlayer.Name}'s Character Sheet",
                        TemplateId = templateId,
                        Owner = newPlayer.Id,
                    },
                    Player = creator,
                    GameID = game.Id,
                    IsCustomUi = false,
                    IsTemplate = false,
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create default character sheet for player {PlayerId} in game {GameId}", newPlayer.Id, game.Id);
            }
        }
    }
}


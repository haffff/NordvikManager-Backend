using MediatR;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Chat.GetMessages;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Layouts.GetLayout;
using NordvikManager.Application.Commands.Layouts.GetLayouts;
using NordvikManager.Application.Commands.TreeEntry.GetTreeEntries;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/BattleMap: the game a player sees (BattleMapController delegates here).</summary>
    public sealed class BattleMapEndpoints
    {
        private readonly IMediator mediator;

        public BattleMapEndpoints(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>The caller's player in the game, or Unauthorized.</summary>
        [ApiRoute("GET", "api/BattleMap/GetPlayer")]
        public async Task<ApiResult> GetPlayer(User? user, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            return ApiResult.Ok(player?.Player);
        }

        /// <summary>The whole game: everything for the GM, only what is visible for a player.</summary>
        [ApiRoute("GET", "api/BattleMap/GetFullGame")]
        public async Task<ApiResult> GetFullGame(User? user, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            var game = await mediator.Send(new GetGameCommand { GameID = gameId, PlayerID = player?.Player?.Id ?? default });
            if (game != null)
                game.IsLocalAdmin = user?.IsLocalAdmin == true;

            return ApiResult.Ok(game);
        }

        /// <summary>Chat, newest first: <paramref name="size"/> messages, <paramref name="page"/> skipped.</summary>
        [ApiRoute("GET", "api/BattleMap/GetChat")]
        public async Task<ApiResult> GetChat(User? user, Guid gameId, int size = 30, int page = 0, Guid from = default, string filter = "")
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            var chat = await mediator.Send(new GetMessagesCommand
            {
                GameID = gameId,
                Size = size,
                Page = page,
                Filter = filter,
                From = from,
                PlayerID = player.Player?.Id ?? default,
            });

            return ApiResult.Ok(chat);
        }

        [ApiRoute("GET", "api/BattleMap/GetLayout")]
        public async Task<ApiResult> GetLayout(User? user, Guid gameId, Guid id)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            return ApiResult.Ok(await mediator.Send(new GetLayoutCommand { Id = id, Player = player.Player }));
        }

        [ApiRoute("GET", "api/BattleMap/GetBattlemap")]
        public async Task<ApiResult> GetBattlemap(User? user, Guid gameId, Guid id)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            return ApiResult.Ok(await mediator.Send(new GetBattleMapCommand { Id = id, Player = player.Player }));
        }

        [ApiRoute("GET", "api/BattleMap/GetBattlemaps")]
        public async Task<ApiResult> GetBattlemaps(User? user, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            return ApiResult.Ok(await mediator.Send(new GetBattleMapsCommand { Player = player.Player, GameID = gameId }));
        }

        [ApiRoute("GET", "api/BattleMap/GetLayouts")]
        public async Task<ApiResult> GetLayouts(User? user, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            return ApiResult.Ok(await mediator.Send(new GetLayoutsCommand { GameID = gameId, Player = player.Player, Flat = true }));
        }

        [ApiRoute("GET", "api/BattleMap/GetCards")]
        public async Task<ApiResult> GetCards(User? user, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot get cards. You are not a player" });

            var result = await mediator.Send(new GetAllCardsCommand { Player = player.Player, Templates = false, CustomUis = false });

            return ApiResult.Ok(result.Item2);
        }

        [ApiRoute("GET", "api/BattleMap/GetTree")]
        public async Task<ApiResult> GetTree(User? user, Guid gameId, string entityType)
        {
            var player = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });

            return ApiResult.Ok(await mediator.Send(new GetTreeEntriesCommand { EntityType = entityType, GameId = gameId, PlayerId = player.Player.Id }));
        }
    }
}

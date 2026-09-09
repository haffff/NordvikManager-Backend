using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.GetGameCentralSessionId;
using DndOnePlaceManager.Application.Commands.Game.GetGameSessionDetails;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Game.SetGameCentralSession;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.RealSession
{
    /// <summary>
    /// Mirrors DNDOnePlaceManager/Controllers/SessionController.cs's StartSession action
    /// (login -> ensure a Central Server session exists for the game -> pre-create the
    /// lobby -> IWebRTCSessionService.StartSessionAsync) against real infrastructure:
    /// a real Central Server login, a real Central Server session, and (unlike every
    /// harness so far) the real ISignalingService — see Program.cs's mode fork. Never
    /// returns under normal operation; this is the process that stays alive to be the
    /// GM backend for whoever connects.
    /// </summary>
    public static class RealGmSession
    {
        // Same account CentralSignalingSpike already proved works against the local
        // Central Server (real login + real session + real signaling auth).
        private const string GmUsername = "poc";
        private const string GmPassword = "pocpocpoc";

        public static async Task RunAsync(IServiceProvider provider)
        {
            var centralServer = provider.GetRequiredService<ICentralServerService>();

            Console.WriteLine($"[RealGmSession] Logging in as '{GmUsername}' against the real Central Server...");
            var login = await centralServer.LoginAsync(GmUsername, GmPassword);
            if (login == null)
            {
                Console.WriteLine("[RealGmSession] Login failed — is the Central Server running and reachable? Aborting.");
                return;
            }
            Console.WriteLine($"[RealGmSession] Logged in as userId={login.UserId} userName={login.UserName}.");

            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var gmUser = new User { Id = login.UserId };

            var gameId = await mediator.Send(new AddGameCommand
            {
                User = gmUser,
                Name = "wasm-gm-real-session",
                PasswordRequired = false,
                IsPublic = false,
            });
            if (gameId == null)
            {
                Console.WriteLine("[RealGmSession] AddGameCommand returned null — could not create the local game. Aborting.");
                return;
            }
            Console.WriteLine($"[RealGmSession] Local game created: gameId={gameId}");

            var centralSessionId = await mediator.Send(new GetGameCentralSessionIdCommand { GameID = gameId.Value });
            if (centralSessionId == null)
            {
                var details = await mediator.Send(new GetGameSessionDetailsCommand { GameId = gameId.Value });
                if (details == null)
                {
                    Console.WriteLine("[RealGmSession] GetGameSessionDetailsCommand returned null right after creating the game — aborting.");
                    return;
                }

                centralSessionId = await centralServer.CreateSessionAsync(login.CentralToken, new GameItemDTO
                {
                    Name = details.Name,
                    ShortDescription = details.Summary,
                    LongDescription = details.Description,
                    PasswordRequired = details.PasswordRequired,
                    IsPublic = details.IsPublic,
                });

                if (centralSessionId == null)
                {
                    Console.WriteLine("[RealGmSession] CreateSessionAsync returned null — Central Server unreachable or rejected the request. Aborting.");
                    return;
                }

                await mediator.Send(new SetGameCentralSessionCommand { GameId = gameId.Value, CentralSessionId = centralSessionId });
            }
            Console.WriteLine($"[RealGmSession] Central Server session: centralSessionId={centralSessionId}");

            var systemPlayer = await mediator.Send(new GetSystemPlayerCommand { GameID = gameId.Value });
            SessionGlue.StartSession(provider, gameId.Value, systemPlayer);

            var sessionService = provider.GetRequiredService<IWebRTCSessionService>();
            Console.WriteLine("[RealGmSession] Starting the real WebRTC session (real signaling)...");
            await sessionService.StartSessionAsync(gameId.Value, centralSessionId, login.CentralToken);

            Console.WriteLine("=====================================================================");
            Console.WriteLine($"[RealGmSession] READY. Open NordvikManagerFrontEnd, log in as '{GmUsername}'/'{GmPassword}',");
            Console.WriteLine("[RealGmSession] and join the game named 'wasm-gm-real-session' from the game list.");
            Console.WriteLine($"[RealGmSession] centralSessionId={centralSessionId}  localGameId={gameId}");
            Console.WriteLine("[RealGmSession] Leave this tab open — it is the GM backend for that session.");
            Console.WriteLine("=====================================================================");

            // Keep the process (browser tab) alive indefinitely to keep listening for peers.
            await Task.Delay(Timeout.Infinite);
        }
    }
}

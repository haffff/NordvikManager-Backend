//using DndOnePlaceManager.Application.DataTransferObjects.Game;
//using DNDOnePlaceManager.WebSockets;
//using Microsoft.Extensions.Logging;
//using System;

//namespace DNDOnePlaceManager.Services.Implementations
//{
//    public class WebSocketLogger : ILogger
//    {
//        private readonly IWebSocketManager manager;
//        private readonly Guid game;
//        private readonly PlayerDTO player;

//        public WebSocketLogger(IWebSocketManager manager, Guid game, PlayerDTO player)
//        {
//            this.manager = manager;
//            this.game = game;
//            this.player = player;
//        }

//        public IDisposable BeginScope<TState>(TState state)
//        {
//            return new NoopDisposable();
//        }

//        public bool IsEnabled(LogLevel logLevel)
//        {
//            return true;
//        }

//        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
//        {
//            var message = formatter(state, exception);
//            manager.SendLogInformation(message,"",logLevel,game,player?.Id);
//        }

//        private class NoopDisposable : IDisposable
//        {
//            public void Dispose()
//            {
//            }
//        }
//    }
//}

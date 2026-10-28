using NordvikManager.Application.DataTransferObjects.Game;
using System;

namespace NordvikManager.Services.Implementations.HookArgs
{
    public class UIHookArgs : HookArgs
    {
        public PlayerDTO Player { get; set; }
        public Guid? BattleMapId { get; set; }
    }
}

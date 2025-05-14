using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;

namespace DNDOnePlaceManager.Services.Implementations.HookArgs
{
    public class UIHookArgs : HookArgs
    {
        public PlayerDTO Player { get; set; }
        public Guid? BattleMapId { get; set; }
    }
}

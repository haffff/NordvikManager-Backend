using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DNDOnePlaceManager.Services.Implementations.HookArgs
{
    /// <summary>
    /// Args for Hook.ChatCommand. For "/cast fireball 3": ChatCommand = "cast",
    /// ChatArgs = "fireball 3", ChatText = "/cast fireball 3".
    /// </summary>
    public class ChatCommandHookArgs : HookArgs
    {
        public PlayerDTO Player { get; set; }
        public string ChatCommand { get; set; }
        public string ChatArgs { get; set; }
        public string ChatText { get; set; }
    }
}

using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;

namespace DndOnePlaceManager.Application.Commands.Chat.RollDices
{
    public class RollDicesCommand : CommandBase<RollDefinition>
    {
        public string DiceString { get; set; }
    }
}

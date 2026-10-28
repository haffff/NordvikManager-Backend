using NordvikManager.Application.Services.Implementations.ChatTemplates;

namespace NordvikManager.Application.Commands.Chat.RollDices
{
    public class RollDicesCommand : CommandBase<RollDefinition>
    {
        public string DiceString { get; set; }
    }
}

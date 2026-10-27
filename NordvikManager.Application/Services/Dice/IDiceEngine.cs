using NordvikManager.Application.Services.Implementations.ChatTemplates;

namespace NordvikManager.Application.Services.Dice
{
    public interface IDiceEngine
    {
        RollDefinition Evaluate(string expression);
    }
}

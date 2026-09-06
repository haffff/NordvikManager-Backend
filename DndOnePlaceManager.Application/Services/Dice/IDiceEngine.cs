using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;

namespace DndOnePlaceManager.Application.Services.Dice
{
    public interface IDiceEngine
    {
        RollDefinition Evaluate(string expression);
    }
}

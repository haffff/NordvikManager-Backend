using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;

namespace DndOnePlaceManager.Application.Services.Interfaces
{
    public interface IChatService
    {
        RollDefinition HandleRoll(string roll);
        string ParseRollFromUser(string roll, string? template = null);
    }
}

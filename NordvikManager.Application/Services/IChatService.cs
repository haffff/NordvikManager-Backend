using NordvikManager.Application.Services.Implementations.ChatTemplates;

namespace NordvikManager.Application.Services.Interfaces
{
    public interface IChatService
    {
        RollDefinition HandleRoll(string roll);
        string ParseRollFromUser(string roll, string? template = null);
    }
}

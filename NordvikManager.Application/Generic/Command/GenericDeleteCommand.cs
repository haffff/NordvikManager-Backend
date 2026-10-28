using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Generic.Command
{
    public class GenericDeleteCommand : GamePlayerCommandBase<CommandResponse>
    {
        public Guid Id { get; set; }
    }
}

using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Generic.Command
{
    public class GenericDeleteCommand : GamePlayerCommandBase<CommandResponse>
    {
        public Guid Id { get; set; }
    }
}

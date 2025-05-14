using DndOnePlaceManager.Application.DataTransferObjects;

namespace DndOnePlaceManager.Application.Generic.Command
{
    public class GenericGetCommand<TDTO> : GamePlayerCommandBase<TDTO> where TDTO : IGameDataTransferObject
    {
        public Guid Id { get; set; }
    }
}

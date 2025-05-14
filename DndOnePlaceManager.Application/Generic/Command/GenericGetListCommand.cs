using DndOnePlaceManager.Application.DataTransferObjects;

namespace DndOnePlaceManager.Application.Generic.Command
{
    public class GenericGetListCommand<TDTO> : GamePlayerCommandBase<List<TDTO>> where TDTO : IGameDataTransferObject
    {

    }
}

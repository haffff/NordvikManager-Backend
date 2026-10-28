using NordvikManager.Application.DataTransferObjects;

namespace NordvikManager.Application.Generic.Command
{
    public class GenericGetListCommand<TDTO> : GamePlayerCommandBase<List<TDTO>> where TDTO : IGameDataTransferObject
    {

    }
}

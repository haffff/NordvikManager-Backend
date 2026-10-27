using NordvikManager.Application.DataTransferObjects;

namespace NordvikManager.Application.Generic.Command
{
    public class GenericGetCommand<TDTO> : GamePlayerCommandBase<TDTO> where TDTO : IGameDataTransferObject
    {
        public Guid Id { get; set; }
    }
}

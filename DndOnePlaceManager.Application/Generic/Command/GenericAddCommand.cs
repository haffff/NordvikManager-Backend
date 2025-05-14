using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Generic.Command
{
    public class GenericAddCommand<TDto> : GamePlayerCommandBase<(CommandResponse, Guid)> where TDto : IGameDataTransferObject
    {
        public TDto Dto { get; set; }
    }
}

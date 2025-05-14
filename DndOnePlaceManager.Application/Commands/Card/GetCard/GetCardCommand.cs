using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Card.GetCard
{
    public class GetCardCommand : GenericGetCommand<CardDto>
    {
        public string? Name { get; set; }
    }
}

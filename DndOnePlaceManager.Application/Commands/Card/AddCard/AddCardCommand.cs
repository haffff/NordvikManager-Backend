using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Card.AddCard
{
    public class AddCardCommand : GenericAddCommand<CardDto>
    {
        public bool IsTemplate { get; set; }
        public bool IsCustomUi { get; set; }
    }
}

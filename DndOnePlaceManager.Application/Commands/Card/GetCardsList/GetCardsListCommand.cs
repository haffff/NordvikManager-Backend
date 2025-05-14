using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Card.GetAllCards
{
    public class GetAllCardsCommand : CommandBase<(CommandResponse, List<CardDto>)>
    {
        public Guid GameId { get; set; }
        public bool Templates { get; set; }
        public bool CustomUis { get; set; }
		public PlayerDTO Player { get; set; }
        public bool Flat { get; set; } = false;
    }
}

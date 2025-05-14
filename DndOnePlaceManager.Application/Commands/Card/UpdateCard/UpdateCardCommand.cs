using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Card.UpdateCard
{
    public class UpdateCardCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
		public CardDto Dto { get; set; }
    }
}

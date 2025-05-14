using AutoMapper;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Chat.RollDices
{
    internal class RollDicesCommandHandler : HandlerBase<RollDicesCommand, RollDefinition>
    {
        private readonly IChatService chatService;

        public RollDicesCommandHandler(IDbContext dbContext, IMapper mapper, IChatService chatService) : base(dbContext, mapper)
        {
            this.chatService = chatService;
        }

        public override async Task<RollDefinition> Handle(RollDicesCommand request, CancellationToken cancellationToken)
        {
            var result = chatService.HandleRoll(request.DiceString);

            if (result == null)
            {
                throw new Exception("Error while rolling dices");
            }

            return await Task.FromResult(result);
        }
    }
}

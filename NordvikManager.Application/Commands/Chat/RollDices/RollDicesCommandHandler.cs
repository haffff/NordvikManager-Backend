using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services.Implementations.ChatTemplates;
using NordvikManager.Application.Services.Interfaces;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Chat.RollDices
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

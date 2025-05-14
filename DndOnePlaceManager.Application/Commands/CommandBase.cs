using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DndOnePlaceManager.Application.Commands
{
    public class CommandBase<TResponse> : IRequest<TResponse>
    {
        public IServiceScope? Scope { get; set; }
    }
}

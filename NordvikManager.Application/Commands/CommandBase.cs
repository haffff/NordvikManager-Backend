using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace NordvikManager.Application.Commands
{
    public class CommandBase<TResponse> : IRequest<TResponse>
    {
        public IServiceScope? Scope { get; set; }
    }
}

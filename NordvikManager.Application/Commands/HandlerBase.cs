using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace NordvikManager.Application.Commands
{
    public class HandlerBase<TCom, TResponse> : IRequestHandler<TCom, TResponse> where TCom : CommandBase<TResponse>
    {
        protected IDbContext dbContext;
        protected IMapper mapper;

        public HandlerBase(IDbContext dbContext, IMapper mapper)
        {
            this.dbContext = dbContext;
            this.mapper = mapper;
        }

        public virtual async Task<TResponse> Handle(TCom request, CancellationToken cancellationToken)
        {
            if (request.Scope != null)
            {
                dbContext = request.Scope.ServiceProvider.GetRequiredService<IDbContext>();
            }

            return default;
        }
    }
}

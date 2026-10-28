using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Helpers;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Elements.GetElementDetails
{
    public class GetElementDetailsCommandHandler : HandlerBase<GetElementDetailsCommand, Dictionary<string, object>>
    {
        public GetElementDetailsCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<Dictionary<string, object>> Handle(GetElementDetailsCommand request, CancellationToken cancellationToken)
        {
            var element = dbContext.Elements.Include(x => x.Details).FirstOrDefault(x => x.Id == request.ElementId);

            Guard.NotFound(element, "Element", request.ElementId);

            element.ThrowIfNoPermission(request.Player?.Id ?? default);

            var filteredDetails = element.Details.Where(x => x.Key.ToLower() == request.Name.ToLower());

            if (!filteredDetails.Any())
            {
                throw new ResourceNotFoundException("ElementDetail", request.Name);
            }

            var details = filteredDetails.ToDictionary(x => x.Key, x => DetailsParseHelper.ParseValueType(x.Value, x.Type));

            return details;
        }
    }
}

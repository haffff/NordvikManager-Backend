using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Helpers;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Elements.GetElementDetails
{
    public class GetElementDetailsCommandHandler : HandlerBase<GetElementDetailsCommand, Dictionary<string, object>>
    {
        public GetElementDetailsCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<Dictionary<string, object>> Handle(GetElementDetailsCommand request, CancellationToken cancellationToken)
        {
            var element = dbContext.Elements.Include(x => x.Details).FirstOrDefault(x => x.Id == request.ElementId);

            if (element == null)
            {
                throw new ResourceNotFoundException(nameof(element));
            }

            element.ThrowIfNoPermission(request.Player?.Id ?? default);

            var filteredDetails = element.Details.Where(x => x.Key.ToLower() == request.Name.ToLower());

            if (!filteredDetails.Any())
            {
                throw new ResourceNotFoundException(nameof(filteredDetails));
            }

            var details = filteredDetails.ToDictionary(x => x.Key, x => DetailsParseHelper.ParseValueType(x.Value, x.Type));

            return details;
        }
    }
}

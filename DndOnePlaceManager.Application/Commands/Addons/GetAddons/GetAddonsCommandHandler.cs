using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Addons.GetAddons
{
    internal class GetAddonsCommandHandler : HandlerBase<GetAddonsCommand, List<AddonDto>>
    {
        public GetAddonsCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<List<AddonDto>> Handle(GetAddonsCommand request, CancellationToken token)
        {
            await base.Handle(request, token);

            var game = dbContext.Games.Include(x => x.Addons).FirstOrDefault(x => x.Id == request.GameId);
            var addons = game.Addons.ToList();

            if (!game.HasPermission(request.Player.Id ?? default, Permission.Edit))
            {
                return new List<AddonDto>();
            }

            if (request.Flat)
            {
                return addons.Select(x => new AddonDto()
                {
                    Name = x.Name,
                    Key = x.Key,
                    Author = x.Author,
                    License = x.License,
                    Description = x.Description,
                    ReleaseUrl = x.ReleaseUrl,
                    RepositoryUrl = x.RepositoryUrl,
                    Version = x.Version,
                    Id = x.Id,
                }).ToList();
            }

            return addons.Select(x => mapper.Map<AddonDto>(x)).ToList();
        }

    }
}

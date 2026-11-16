using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Addons.GetAddons
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

            // Addons whose actions send outside requests (one query, ids only): those steps
            // don't work on the browser version, whatever the addon declares.
            var ids = addons.Select(a => a.Id).ToList();
            var goOutside = dbContext.Addons
                .Where(a => ids.Contains(a.Id) && a.Actions!.Any(x => x.Content.Contains("SendRequest") || x.Content.Contains("DownloadResource")))
                .Select(a => a.Id)
                .ToHashSet();

            List<AddonDto> dtos;
            if (request.Flat)
            {
                dtos = addons.Select(x => new AddonDto()
                {
                    Name = x.Name,
                    Key = x.Key,
                    Author = x.Author,
                    Website = x.Website,
                    License = x.License,
                    Description = x.Description,
                    ReleaseUrl = x.ReleaseUrl,
                    RepositoryUrl = x.RepositoryUrl,
                    Version = x.Version,
                    Id = x.Id,
                    IsEnabled = x.IsEnabled,
                    Dependencies = x.Dependencies?.Select(d => new AddonDependencyDto { Key = d.Key, Version = d.Version }).ToList() ?? new(),
                    Network = x.Network == null ? null : new AddonNetworkDto { Hosts = x.Network.Hosts?.ToList() ?? new(), UserHosts = x.Network.UserHosts },
                }).ToList();
            }
            else
            {
                dtos = addons.Select(x => mapper.Map<AddonDto>(x)).ToList();
            }

            foreach (var dto in dtos)
                dto.UsesOutsideConnections = dto.Id is Guid id && goOutside.Contains(id);
            return dtos;
        }

    }
}

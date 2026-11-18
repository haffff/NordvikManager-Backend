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

            // Addons whose actions send outside requests: those may not work on the browser
            // version, whatever the addon declares. The text search only narrows it down; a
            // step of that type (anywhere, e.g. in an If's branch) decides.
            var ids = addons.Select(a => a.Id).ToList();
            var maybe = await dbContext.Addons
                .Where(a => ids.Contains(a.Id))
                .SelectMany(a => a.Actions!
                    .Where(x => x.Content.Contains("SendRequest") || x.Content.Contains("DownloadResource"))
                    .Select(x => new { AddonId = a.Id, x.Content }))
                .ToListAsync(token);
            var goOutside = maybe.Where(m => HasOutsideStep(m.Content)).Select(m => m.AddonId).ToHashSet();

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

        private static readonly HashSet<string> OutsideSteps = new(StringComparer.OrdinalIgnoreCase) { "SendRequest", "DownloadResource" };

        private static bool HasOutsideStep(string? content)
        {
            Newtonsoft.Json.Linq.JToken root;
            try
            {
                root = Newtonsoft.Json.Linq.JToken.Parse(content ?? "");
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
            return ((root as Newtonsoft.Json.Linq.JContainer)?.DescendantsAndSelf() ?? Enumerable.Empty<Newtonsoft.Json.Linq.JToken>())
                .OfType<Newtonsoft.Json.Linq.JObject>()
                .Any(o => o.GetValue("type", StringComparison.OrdinalIgnoreCase) is Newtonsoft.Json.Linq.JValue { Type: Newtonsoft.Json.Linq.JTokenType.String } type
                    && OutsideSteps.Contains((string)type!));
        }

    }
}

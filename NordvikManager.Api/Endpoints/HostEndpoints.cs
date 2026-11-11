using NordvikManager.Domain.Entities.Auth;
using NordvikManager.WebRTC;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>
    /// api/Host (HostController delegates here): what kind of host the GM is on, and what it
    /// doesn't provide. A host without the server features lists every server-only route with its
    /// reason (<see cref="ApiNativeOnly"/>, [ApiRoute(NativeOnly = true, Reason = ...)]), so the
    /// list can't drift from what the router actually refuses.
    /// </summary>
    public sealed class HostEndpoints
    {
        [ApiRoute("GET", "api/Host/Info")]
        public ApiResult Info(User? user, [ApiService] IHostDescription host)
        {
            if (user == null)
                return ApiResult.Unauthorized();

            var unavailable = host.HasServerFeatures
                ? new List<HostUnavailableFeature>()
                : ServerOnly(ApiRoutes.Shared);
            return ApiResult.Ok(new HostInfoResponse(host.Kind, host.Version, ProtocolVersion.Current, unavailable, host.Limitations));
        }

        /// <summary>Every server-only controller and route, with why, by path.</summary>
        public static List<HostUnavailableFeature> ServerOnly(ApiRoutes routes)
            => ApiNativeOnly.Controllers.Select(c => new HostUnavailableFeature("api/" + c.Key, c.Value))
                .Concat(routes.All.Where(r => r.NativeOnly).Select(r => new HostUnavailableFeature(r.Path, r.Reason ?? "")))
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}

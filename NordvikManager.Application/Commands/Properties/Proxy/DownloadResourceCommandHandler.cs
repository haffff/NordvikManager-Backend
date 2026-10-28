using NordvikManager.Application.Commands.Resources.SetResource;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Proxy;
using MediatR;

namespace NordvikManager.Application.Commands.Properties.Proxy
{
    public class DownloadResourceCommandHandler : HandlerBase<DownloadResourceCommand, DownloadResourceResult>
    {
        private readonly IProxyHttpService proxyHttpService;
        private readonly ProxyOptions proxyOptions;
        private readonly IMediator mediator;

        public DownloadResourceCommandHandler(IDbContext dbContext, IMapper mapper, IProxyHttpService proxyHttpService, ProxyOptions proxyOptions, IMediator mediator)
            : base(dbContext, mapper)
        {
            this.proxyHttpService = proxyHttpService;
            this.proxyOptions = proxyOptions;
            this.mediator = mediator;
        }

        public override async Task<DownloadResourceResult> Handle(DownloadResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (!await ProxySupport.IsUrlAllowedAsync(request.TargetUrl, proxyOptions))
                return new DownloadResourceResult { Success = false, StatusCode = 400 };

            var resolvedProtected = ProxySupport.ResolveProtected(dbContext, request.ParentID, new[] { request.BearerTokenPropertyName });
            string? bearerToken = null;
            if (request.BearerTokenPropertyName != null)
                resolvedProtected.TryGetValue(request.BearerTokenPropertyName, out bearerToken);

            var (success, statusCode, body, contentType) = await proxyHttpService.GetBytesAsync(request.TargetUrl, bearerToken, cancellationToken);
            if (!success || body == null)
                return new DownloadResourceResult { Success = false, StatusCode = statusCode };

            var mimeType = (string.IsNullOrWhiteSpace(request.MimeType) ? contentType : request.MimeType)?.Split(';')[0].Trim();
            var resourceId = await mediator.Send(new SetResourceCommand
            {
                GameId = request.GameId,
                Player = request.Player,
                Key = request.Key,
                Name = string.IsNullOrWhiteSpace(request.Name) ? request.Key : request.Name,
                Data = body,
                MimeType = mimeType,
                ParentFolder = request.ParentFolder,
            }, cancellationToken);

            // Resources keep only the types they know (SetResource saves the rest without one).
            var known = mimeType?.ToEnumUsingDescriptionAttribute<MimeType>() is MimeType m && m != MimeType.None;
            return new DownloadResourceResult
            {
                Success = true,
                StatusCode = statusCode,
                ResourceId = resourceId,
                MimeType = known ? mimeType : null,
                Size = body.LongLength,
            };
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;

namespace DndOnePlaceManager.Infrastructure.Services.Proxy
{
    /// <summary>
    /// The HttpClient action requests go through. Its connect step resolves the host,
    /// applies ProxyAddressRules and connects to exactly the addresses it checked, so a
    /// redirect to a refused host or a second, different DNS answer can't get past the rule.
    /// </summary>
    public static class ProxyHttpClient
    {
        public const string Name = "proxy";

        public static IHttpClientBuilder AddProxyHttpClient(this IServiceCollection services) =>
            services.AddHttpClient(Name, (sp, client) =>
                {
                    var options = sp.GetRequiredService<ProxyOptions>();
                    client.Timeout = options.Timeout;
                    client.MaxResponseContentBufferSize = options.MaxResponseBytes;
                })
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var options = sp.GetRequiredService<ProxyOptions>();
                    return new SocketsHttpHandler
                    {
                        AllowAutoRedirect = options.MaxRedirects > 0,
                        MaxAutomaticRedirections = Math.Max(1, options.MaxRedirects),
                        ConnectCallback = (context, cancellationToken) => ConnectAsync(context, options, cancellationToken),
                    };
                });

        private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, ProxyOptions options, CancellationToken cancellationToken)
        {
            var (host, port) = (context.DnsEndPoint.Host, context.DnsEndPoint.Port);
            var addresses = await ProxyAddressRules.ResolveAsync(host, cancellationToken);
            if (!ProxyAddressRules.IsAllowed(host, port, addresses, options))
                throw new HttpRequestException(
                    $"'{host}' is a private or local address. The server's owner can allow it in {ProxyOptions.ConfigKey}.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}

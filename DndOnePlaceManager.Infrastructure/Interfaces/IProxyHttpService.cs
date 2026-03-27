namespace DndOnePlaceManager.Infrastructure.Interfaces
{    public interface IProxyHttpService
    {
        /// <summary>
        /// Sends a JSON request to the given URL using the specified HTTP method.
        /// When <paramref name="bearerToken"/> is provided it is set as the Authorization header.
        /// Body is ignored for GET and HEAD requests.
        /// </summary>
        Task<(bool Success, int StatusCode, string? ResponseBody)> SendJsonAsync(
            string method,
            string url,
            Dictionary<string, object?> body,
            string? bearerToken = null,
            CancellationToken cancellationToken = default);
    }
}

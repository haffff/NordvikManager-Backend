namespace NordvikManager.Services.Interfaces
{
    /// <summary>
    /// Where the GM host keeps its Central refresh token, for <see cref="ICentralServerService"/>
    /// to refresh an expired access token. The native server: the GM's cookies on the current
    /// request. The browser-wasm host: its own login (<see cref="HeldCentralTokens"/>).
    /// </summary>
    public interface ICentralTokenStore
    {
        /// <summary>The refresh token to use now, or null when there is none.</summary>
        string? RefreshToken { get; }

        /// <summary>Tokens a refresh produced: Central revoked the old refresh token, so keep these.</summary>
        void Refreshed(CentralTokens tokens);
    }

    /// <summary>The tokens a host holds itself (no HTTP request carries them), as last logged in or refreshed.</summary>
    public sealed class HeldCentralTokens : ICentralTokenStore
    {
        private volatile CentralTokens? _tokens;

        public string? AccessToken => _tokens?.AccessToken;

        public string? RefreshToken => _tokens?.RefreshToken;

        public void Set(CentralTokens tokens) => _tokens = tokens;

        public void Clear() => _tokens = null;

        // A refresh without a new refresh token keeps the one held.
        public void Refreshed(CentralTokens tokens) => _tokens = tokens with { RefreshToken = tokens.RefreshToken ?? _tokens?.RefreshToken };
    }
}

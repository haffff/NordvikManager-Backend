namespace NordvikManager.Application.Commands.Properties.Proxy
{
    /// <summary>
    /// Why an action's outside request (Send Request, Download Resource) didn't happen: its
    /// result's <c>Reason</c>, null when it went out (whatever the other side answered). The
    /// game's GMs are told about each one (the step's AddonNetworkNotice).
    /// </summary>
    public static class ProxyReasons
    {
        /// <summary>The address isn't allowed: not http(s), or a private/local host the server doesn't list.</summary>
        public const string BlockedHost = "blocked-host";

        /// <summary>The action's addon declares the hosts it connects to, and this isn't one.</summary>
        public const string UndeclaredHost = "undeclared-host";

        /// <summary>This GM host can't connect outside its own site (the browser version).</summary>
        public const string NoOutsideConnections = "no-outside-connections";

        /// <summary>It went out but failed on the way (DNS, refused, timed out).</summary>
        public const string NetworkError = "network-error";
    }
}

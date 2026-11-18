namespace NordvikManager.Controllers
{
    /// <summary>GET api/Security/sandboxorigins: the hosts addon cards and Html chat messages may load from.</summary>
    public class SandboxOriginsResponse
    {
        public IReadOnlyList<string> Origins { get; set; } = new List<string>();
    }
}

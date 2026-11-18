namespace NordvikManager.Models
{
    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Username { get; set; }
        public string InviteCode { get; set; }
        /// <summary>The answer to Central's signup puzzle (ALTCHA), passed on as it came.</summary>
        public string? Altcha { get; set; }
        /// <summary>Central's hidden honeypot field: a person leaves it empty. Passed on as it came.</summary>
        public string? NmTrapField { get; set; }
    }
}

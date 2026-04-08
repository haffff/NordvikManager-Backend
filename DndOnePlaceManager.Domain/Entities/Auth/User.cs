namespace DNDOnePlaceManager.Domain.Entities.Auth
{
    public class User
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool? IsAdmin { get; set; }
        public bool Lock { get; set; }
    }
}

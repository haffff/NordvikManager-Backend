using System.ComponentModel.DataAnnotations;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class CreateUserRequest
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
        public bool IsAdmin { get; set; } = false;
    }
}

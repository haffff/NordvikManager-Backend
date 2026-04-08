using System.ComponentModel.DataAnnotations;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class ToggleAdminRequest
    {
        [Required]
        public string UserID { get; set; }
        [Required]
        public bool IsAdmin { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class ResetPasswordRequest
    {
        [Required]
        public string UserID { get; set; }
        [Required]
        public string NewPassword { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace NordvikManager.Controllers.Requests
{
    public class ResetPasswordRequest
    {
        [Required]
        public string UserID { get; set; }
        [Required]
        public string NewPassword { get; set; }
    }
}

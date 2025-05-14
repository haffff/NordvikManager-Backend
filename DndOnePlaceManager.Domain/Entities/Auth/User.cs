using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDOnePlaceManager.Domain.Entities.Auth
{
    public class User : IdentityUser
    {
        public bool? IsAdmin { get; set; }
        public string KeyBindings { get; set; }

        [NotMapped]
        public bool Lock { get; set; }
    }
}

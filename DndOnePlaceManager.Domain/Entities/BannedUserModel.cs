using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DndOnePlaceManager.Domain.Entities
{
    public class BannedUserModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        /// <summary>Central Server user UUID that is banned from this server.</summary>
        public string CentralServerUserId { get; set; } = string.Empty;

        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    }
}

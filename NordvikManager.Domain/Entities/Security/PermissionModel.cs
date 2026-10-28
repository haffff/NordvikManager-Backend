using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordvikManager.Domain.Entities.Security
{
    public class PermissionModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid ModelID { get; set; }
        public Guid PlayerID { get; set; }
        public bool All { get; set; }
        public Permission Permission { get; set; }
    }
}

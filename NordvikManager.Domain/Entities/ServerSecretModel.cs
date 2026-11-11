using System.ComponentModel.DataAnnotations;

namespace NordvikManager.Domain.Entities
{
    /// <summary>
    /// A random secret this server makes once and keeps in its database (so a backup brings
    /// it along), e.g. the key exports are signed with. Never sent anywhere or printed.
    /// </summary>
    public class ServerSecretModel
    {
        [Key]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>The secret, base64.</summary>
        public string Value { get; set; } = string.Empty;
    }
}

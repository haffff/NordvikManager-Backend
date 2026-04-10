using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Interfaces
{
    public interface ILocalAdminService
    {
        /// <summary>
        /// Checks if a user should be granted local admin privileges.
        /// Handles auto-promotion logic and configuration-based checks.
        /// </summary>
        Task<bool> IsLocalAdminAsync(string userId, string? email);

        /// <summary>
        /// Gets the current local admin user ID if one has been auto-promoted.
        /// </summary>
        string? GetAutoPromotedAdminId();
    }
}

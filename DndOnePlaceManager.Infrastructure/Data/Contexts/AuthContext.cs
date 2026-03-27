using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DndOnePlaceManager.Infrastructure.Data.Contexts
{
    internal class AuthContext : IdentityDbContext<User>, IAuthDBContext
    {
        public AuthContext(DbContextOptions<AuthContext> options, IConfiguration configuration) : base(options)
        {
            Database.EnsureCreated();
            if (!Users.Any())
            {
                User user = new User();
                user.UserName = "admin";
                user.Email = "admin@dndmanager.pl";
                PasswordHasher<User> passwordHasher = new PasswordHasher<User>();
                string adminPass = configuration["InitialAdminPassword"];

                if (string.IsNullOrEmpty(configuration["InitialAdminPassword"]))
                {
                    //generate random password
                    adminPass = Guid.NewGuid().ToString("N").Substring(0, 8);
                    Console.WriteLine($"WARNING: InitialAdminPassword is not set in configuration. Generated password '{adminPass}'.");
                }

                user.PasswordHash = passwordHasher.HashPassword(user, adminPass);
                user.IsAdmin = true;

                user.KeyBindings = configuration["DefaultKeyBindings"] ?? "{}";

                user.NormalizedUserName = user.UserName.ToUpper();

                Users.Add(user);

                SaveChanges();
            }
        }
    }
}

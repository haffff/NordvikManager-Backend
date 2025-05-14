using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using LoginRequest = DNDOnePlaceManager.Models.LoginRequest;

namespace DNDOnePlaceManager.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;

        public AuthService(UserManager<User> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        public async Task<(bool, string)> Register(RegisterRequest registerRequest)
        {
            var userExists = await _userManager.FindByNameAsync(registerRequest.Username);
            if (userExists != null)
            {
                return (false, "User already exists!");
            }

            User user = new User()
            {
                Email = registerRequest.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerRequest.Username,
                KeyBindings = _configuration["DefaultKeyBindings"] ?? "{}"
            };

            var result = await _userManager.CreateAsync(user, registerRequest.Password);

            if (!result.Succeeded)
            {
                return (false, GetErrorsText(result.Errors));
            }
            return (true, "User created successfully!");
        }

        public async Task<string> Login(LoginRequest request, HttpContext context)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(request.Username);

                if (user is null)
                {
                    user = await _userManager.FindByEmailAsync(request.Username);
                }

                if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
                {
                    throw new ArgumentException($"Unable to authenticate user {request.Username}");
                }

                var authClaims = new List<Claim>
                {
                    new(ClaimTypes.Name, user.UserName),
                    new(ClaimTypes.Email, user.Email),
                    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()
                    )
                };

                var token = GetToken(authClaims);

                context.Session.SetString("UserID", user.Id);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private JwtSecurityToken GetToken(IEnumerable<Claim> authClaims)
        {

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWTSecret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddHours(3),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256));

            return token;
        }

        private string GetErrorsText(IEnumerable<IdentityError> errors)
        {
            return string.Join(", \r\n", errors.Select(error => error.Description).ToArray());
        }

        public async Task<bool> IsAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return user.IsAdmin ?? false;
        }

        public async Task<string> GetUserName(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return user.UserName;
        }

        public (List<object> users, int usersTotal) GetUsers(int page, int count)
        {
            return (_userManager.Users.Skip(page * count).Take(count).Select(x => new
            {
                x.Id,
                x.UserName,
                x.Email,
                x.IsAdmin
            }).Cast<object>().ToList(), 
            _userManager.Users.Count());
        }

        public async Task<Dictionary<string,string>> GetKeyboardBindings(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(user.KeyBindings);
            return deserialized;
        }

        public async Task<bool> SetKeyboardBindings(string id, Dictionary<string, string> bindings)
        {
            string serializedBindings = JsonConvert.SerializeObject(bindings);
            var user = await _userManager.FindByIdAsync(id);
            user.KeyBindings = serializedBindings;
            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }
    }
}

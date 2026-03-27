using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Interfaces
{
    public interface IAuthService
    {
        public Task<string> GetUserName(string id);
        public Task<string> Login(LoginRequest request, HttpContext context);
        public Task<bool> IsAdmin(string id);
        public Task<(bool,string)> Register(RegisterRequest registerRequest);
        (List<object> users, int usersTotal) GetUsers(int page, int count);        Task<bool> SetKeyboardBindings(string id, Dictionary<string, string> bindings);
        Task<Dictionary<string, string>> GetKeyboardBindings(string id);        Task<bool> DeleteUser(string id);
        Task<bool> ToggleAdmin(string id, bool isAdmin);
        Task<bool> ResetPassword(string id, string newPassword);
        Task<(bool, string)> CreateUser(string userName, string email, string password, bool isAdmin);
    }
}

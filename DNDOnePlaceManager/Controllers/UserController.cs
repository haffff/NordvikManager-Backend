using DNDOnePlaceManager.Controllers.Responses;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Engine.Attribs;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly IMediator mediator;
        IAuthService authService;
        private readonly IConfiguration configuration;
        private static readonly Dictionary<string, DateTime> registrationInvite = new Dictionary<string, DateTime>();

        public UserController(IMediator mediator, IAuthService auth, IConfiguration configuration)
        {
            this.mediator = mediator;
            authService = auth;
            this.configuration = configuration;

        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] Models.LoginRequest loginData)
        {
            var result = await authService.Login(loginData, HttpContext);

            if (result != null)
            {
                HttpContext.Response.Cookies.Append("Authorization", result, new CookieOptions()
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.None,
                    Secure = true
                });
                return Ok(new { result });
            }

            return Unauthorized("Wrong Pass");
        }

        [Authorize]
        [HttpGet]
        [Route("CheckLogin")]
        public IActionResult CheckLogin()
        {
            return Ok();
        }

        [Authorize]
        [HttpGet]
        [Route("Logout")]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Response.Cookies.Delete("Authorization");

            return Ok();
        }

        [Authorize]
        [HttpGet]
        [Route("UserInfo")]
        public async Task<IActionResult> GetUserInfo()
        {
            var user = HttpContext.Items["User"] as User;
            return Ok(
                new
                {
                    Admin = user.IsAdmin,
                    UserName = user.UserName,
                    Email = user.Email
                });
        }

        [Authorize]
        [HttpGet]
        [Route("GetUserNameById")]
        public async Task<IActionResult> GetUserNameById(string id)
        {
            return Ok(await authService.GetUserName(id));
        }

        [Authorize]
        [HttpGet]
        [Route("Invites")]
        public async Task<IActionResult> Invites(int page, int count = 10)
        {
            var user = HttpContext.Items["User"] as User;

            if (!user?.IsAdmin == true)
                return BadRequest();

            if (page < 1 || count < 1)
                return BadRequest();

            var invites = registrationInvite
                .Skip((page - 1) * count)
                .Take(count)
                .Select(x => new { x.Key, x.Value })
                .ToList();
            return Ok(new PaginatedResponse(page, count, invites, registrationInvite.Count));
        }

        [Authorize]
        [HttpGet]
        [Route("GenerateInvite")]
        public async Task<IActionResult> GenerateInvite(int numberOfUsages = 1, int lifeTime = 24)
        {
            var currentUser = HttpContext.Items["User"] as User;

            if (!currentUser?.IsAdmin ?? false)
            {
                return Unauthorized();
            }

            Guid inviteGuid = Guid.NewGuid();
            Guid inviteGuidSecond = Guid.NewGuid();

            string inviteCode = inviteGuid.ToString().Replace("-", "") + inviteGuidSecond.ToString().Replace("-", "");
            inviteCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(inviteCode));

            registrationInvite.Add(inviteCode, DateTime.Now.AddHours(lifeTime));

            return Ok(new { InviteCode = inviteCode });
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register(Models.RegisterRequest registerRequest)
        {
            var inviteCode = registerRequest.InviteCode;
            if (registrationInvite.ContainsKey(inviteCode))
            {
                if (registrationInvite[inviteCode] > DateTime.Now)
                {

                    (bool success, string message) = await authService.Register(registerRequest);
                    if (success)
                    {
                        registrationInvite.Remove(inviteCode);
                        return Ok(new { message });
                    }

                    return BadRequest(new { message });
                }
                else
                {
                    registrationInvite.Remove(inviteCode);
                    return Unauthorized(new { message = "Invite expired" });
                }
            }

            return Unauthorized(new { message = "Invalid invite code" });
        }

        [HttpGet]
        [Route("CheckRegistrationKey")]
        public async Task<IActionResult> CheckRegistrationKey(string key)
        {
            if (registrationInvite.ContainsKey(key))
            {
                return Ok(new { result = "ok" });
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpGet]
        [Authorize]
        [Route("users")]
        public async Task<IActionResult> Users(int page, int count = 10)
        {
            var user = HttpContext.Items["User"] as User;

            //For now
            if (!user?.IsAdmin == true)
                return BadRequest();

            if (page < 1 || count < 1)
                return BadRequest();

            var (users, usersTotal) = authService.GetUsers(page - 1, count);

            return Ok(new PaginatedResponse()
            {
                Page = page,
                Count = count,
                Total = usersTotal,
                Data = users
            });
        }

        [HttpDelete]
        [Authorize]
        [Route("DeleteInvite")]
        public async Task<IActionResult> DeleteInvite([FromQuery] string key)
        {
            var user = HttpContext.Items["User"] as User;

            //For now
            if (!user?.IsAdmin == true)
                return BadRequest();

            if (registrationInvite.ContainsKey(key))
            {
                registrationInvite.Remove(key);
                return Ok(new { result = "ok" });
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet]
        [Authorize]
        [Route("KeyboardBindings")]
        public async Task<IActionResult> KeyboardBindings()
        {
            var user = HttpContext.Items["User"] as User;

            return Ok(authService.GetKeyboardBindings(user.Id));
        }

        [HttpPost]
        [Authorize]
        [Route("KeyboardBindings")]
        public async Task<IActionResult> SaveKeyboardBindings(Dictionary<string,string> bindings)
        {
            var user = HttpContext.Items["User"] as User;
            var regex = new System.Text.RegularExpressions.Regex(@"(Ctrl\+)*(Alt\+)*(Shift\+)*(.|HOME|DELETE|INSERT|PAGEUP|END|PAGEDOWN|BACKSPACE)$");

            //sanitize bindings

            foreach (var key in bindings.Keys)
            {
                //check if key is valid
                if (!regex.IsMatch(key))
                {
                    return BadRequest();
                }

                //check if value is valid

            }

            if (await authService.SetKeyboardBindings(user.Id, bindings))
            {
                return Ok();
            }
            else
            {
                return BadRequest();
            }
        }
    }
}

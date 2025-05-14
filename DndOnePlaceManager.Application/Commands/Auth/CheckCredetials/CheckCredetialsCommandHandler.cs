//using AutoMapper;
//using DndOnePlaceManager.Infrastructure.Interfaces;
//using DNDOnePlaceManager.Domain.Entities.Auth;
//using MediatR;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;

//namespace DndOnePlaceManager.Application.Commands.Auth
//{
//    internal class CheckCredetialsCommandHandler : IRequestHandler<CheckCredetialsCommand, bool>
//    {
//        public CheckCredetialsCommandHandler(IAuthDBContext battleMapContext, IMapper mapper)
//        {
//        }

//        /// <summary>
//        /// Handles checking credetials
//        /// </summary>
//        /// <param name="request"></param>
//        /// <param name="cancellationToken"></param>
//        /// <returns></returns>
//        public async Task<Guid?> Handle(CheckCredetialsCommand request, CancellationToken cancellationToken)
//        {
//            var user = await _userManager.FindByNameAsync(request.Username);

//            if (user is null)
//            {
//                user = await _userManager.FindByEmailAsync(request.Username);
//            }

//            if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
//            {
//                throw new ArgumentException($"Unable to authenticate user {request.Username}");
//            }

//            var authClaims = new List<Claim>
//        {
//            new(ClaimTypes.Name, user.UserName),
//            new(ClaimTypes.Email, user.Email),
//            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
//        };

//            var token = GetToken(authClaims);

//            return new JwtSecurityTokenHandler().WriteToken(token);
//        }

//        private JwtSecurityToken GetToken(IEnumerable<Claim> authClaims)
//        {
//            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

//            var token = new JwtSecurityToken(
//                issuer: _configuration["JWT:ValidIssuer"],
//                audience: _configuration["JWT:ValidAudience"],
//                expires: DateTime.Now.AddHours(3),
//                claims: authClaims,
//                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256));

//            return token;
//        }
//    }
//}


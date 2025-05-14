
using DNDOnePlaceManager.Domain.Entities.Auth;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DndOnePlaceManager.Application.Commands.Auth
{
    public class GetUserResponse
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
        public bool? IsAdmin { get; set; }
        public User? User { get; internal set; }
    }
}


﻿namespace DNDOnePlaceManager.Models
{
    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Username { get; set; }
        public string InviteCode { get; set; }
    }
}

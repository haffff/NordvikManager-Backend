using System;

namespace DNDOnePlaceManager.Models
{
    public class KickPlayerRequest
    {
        public Guid PlayerId { get; set; }
    }

    public class CentralUserRequest
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}

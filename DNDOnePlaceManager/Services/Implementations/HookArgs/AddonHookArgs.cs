using System;

namespace DNDOnePlaceManager.Services.Implementations.HookArgs
{
    public class AddonHookArgs : HookArgs
    {
        public Guid GameId { get; set; }

        public Guid PlayerId { get; set; }

        public string AddonKey { get; set; }
    }
}

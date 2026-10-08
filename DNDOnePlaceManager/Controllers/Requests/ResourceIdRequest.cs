using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class ResourceIdRequest
    {
        public Guid ResourceId { get; set; }

        /// <summary>PlaySound: the soundboard it was played from, whose volume applies too.</summary>
        public Guid? SoundboardId { get; set; }
    }
}

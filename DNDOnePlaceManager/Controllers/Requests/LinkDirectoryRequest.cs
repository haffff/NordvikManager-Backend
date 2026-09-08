using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class LinkDirectoryRequest
    {
        public string LocalDirectoryPath { get; set; }
        public Guid? ParentFolder { get; set; }
    }
}

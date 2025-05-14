using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Interfaces
{
    public interface IMaterialsService
    {
        public Task<Dictionary<string,string>> GetMaterials(string filter);
        public Task<byte[]> GetMaterial(string name);
    }
}

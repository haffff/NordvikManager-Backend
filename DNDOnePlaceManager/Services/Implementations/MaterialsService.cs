using DNDOnePlaceManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    public class MaterialsService : IMaterialsService
    {
        private const string PathToMaterials = "materials\\dndMaterials";

        public async Task<byte[]> GetMaterial(string name)
        {
            name = Path.Combine(PathToMaterials, name + ".pdf");
            return File.ReadAllBytes(name);
        }

        public async Task<Dictionary<string, string>> GetMaterials(string filter)
        {
            var files =  Directory.GetFiles(PathToMaterials);
            var dict = files.ToDictionary(x => Path.GetFileNameWithoutExtension(x), y => y);
            return dict;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;

namespace DNDOnePlaceManager.Enums
{
    /// <summary>
    /// Reads <see cref="HookMetaAttribute"/> decorations from <see cref="Hook"/> fields
    /// and exposes a serialisable list for the UI.
    /// </summary>
    public static class HookInfo
    {
        private static List<HookInfoDto> _cache;

        /// <summary>
        /// Returns all Hook values that have a <see cref="HookMetaAttribute"/>,
        /// sorted by category then name.
        /// Result is cached after the first call.
        /// </summary>
        public static List<HookInfoDto> GetAll()
        {
            if (_cache != null)
                return _cache;

            var result = new List<HookInfoDto>();
            foreach (var field in typeof(Hook).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var meta = field.GetCustomAttribute<HookMetaAttribute>();
                if (meta == null)
                    continue;

                result.Add(new HookInfoDto
                {
                    Value       = (int)field.GetValue(null),
                    Key         = field.Name,
                    Name        = meta.Name,
                    Description = meta.Description,
                    Category    = meta.Category
                });
            }

            result.Sort((a, b) =>
            {
                int cat = string.Compare(a.Category, b.Category, StringComparison.Ordinal);
                return cat != 0 ? cat : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            _cache = result;
            return _cache;
        }
    }
}

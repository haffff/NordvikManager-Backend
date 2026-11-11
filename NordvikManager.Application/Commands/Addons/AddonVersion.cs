using System.Globalization;

namespace NordvikManager.Application.Commands.Addons
{
    /// <summary>Compares addon versions for dependencies, where a dependency's version is the lowest that works.</summary>
    public static class AddonVersion
    {
        /// <summary>
        /// True when <paramref name="installed"/> is <paramref name="required"/> or newer. Versions
        /// compare part by part as numbers (1.10 is newer than 1.9, 1.0 equals 1.0.0), ignoring a
        /// "-beta" or "+build" suffix. No required version accepts anything; versions that aren't
        /// numbers only match exactly.
        /// </summary>
        public static bool Satisfies(string? installed, string? required)
        {
            if (string.IsNullOrWhiteSpace(required))
                return true;
            if (string.IsNullOrWhiteSpace(installed))
                return false;

            var have = Parse(installed);
            var need = Parse(required);
            if (have == null || need == null)
                return string.Equals(installed.Trim(), required.Trim(), StringComparison.Ordinal);

            for (var i = 0; i < Math.Max(have.Length, need.Length); i++)
            {
                var a = i < have.Length ? have[i] : 0;
                var b = i < need.Length ? need[i] : 0;
                if (a != b)
                    return a > b;
            }
            return true;
        }

        private static long[]? Parse(string version)
        {
            var core = version.Trim().Split('-', '+')[0];
            var parts = core.Split('.');
            var numbers = new long[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                    return null;
            }
            return numbers;
        }
    }

    /// <summary>Which of a game's addons need a given addon.</summary>
    public static class AddonDependents
    {
        /// <summary>Names of the addons in <paramref name="gameAddons"/> (other than the addon itself) that list <paramref name="key"/> as a dependency.</summary>
        public static List<string> Of(IEnumerable<Domain.Entities.AddonModel> gameAddons, string? key, Guid? addonId)
            => gameAddons
                .Where(a => a.Id != addonId && a.Dependencies != null && a.Dependencies.Any(d => d.Key == key))
                .Select(a => a.Name ?? a.Key)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        public static string RefusalMessage(string? name, IEnumerable<string> requiredBy)
            => $"\"{name}\" is needed by {string.Join(", ", requiredBy.Select(n => $"\"{n}\""))}. Uninstall those first.";
    }
}

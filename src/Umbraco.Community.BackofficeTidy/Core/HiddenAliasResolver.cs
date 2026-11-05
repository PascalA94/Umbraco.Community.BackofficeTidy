using Umbraco.Community.BackofficeTidy.Configuration;

namespace Umbraco.Community.BackofficeTidy;

/// <summary>
/// Works out which aliases to hide for one caller. Pure: no state, so it is registered as a singleton and tested directly.
/// </summary>
public sealed class HiddenAliasResolver
{
    /// <summary>
    /// A rule applies when its environments are empty or contain <paramref name="environmentName"/> (ignoring case) and its
    /// user groups are empty or share any entry with <paramref name="groupAliases"/> (ignoring case). Aliases are
    /// de-duplicated exactly and sorted ordinal. Rules the validator would reject are skipped, never thrown on.
    /// </summary>
    public IReadOnlyList<string> Resolve(
        IEnumerable<HideRule> rules,
        string environmentName,
        IReadOnlyCollection<string> groupAliases)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (HideRule rule in rules)
        {
            if (!AppliesTo(rule, environmentName, groupAliases))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(rule.Alias))
            {
                result.Add(rule.Alias.Trim());
            }
            else if (rule.Preset is not null && Presets.All.TryGetValue(rule.Preset, out var aliases))
            {
                result.UnionWith(aliases);
            }
        }

        var sorted = result.ToList();
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    private static bool AppliesTo(HideRule rule, string environmentName, IReadOnlyCollection<string> groupAliases)
        => (rule.Environments.Length == 0
                || rule.Environments.Contains(environmentName, StringComparer.OrdinalIgnoreCase))
           && (rule.UserGroups.Length == 0
                || rule.UserGroups.Any(g => groupAliases.Contains(g, StringComparer.OrdinalIgnoreCase)));
}

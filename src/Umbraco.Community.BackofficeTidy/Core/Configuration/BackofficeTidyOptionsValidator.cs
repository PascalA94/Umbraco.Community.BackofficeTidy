using Microsoft.Extensions.Options;

namespace Umbraco.Community.BackofficeTidy.Configuration;

/// <summary>Fails start-up on a malformed rule. Every message names the rule by its configuration path.</summary>
public sealed class BackofficeTidyOptionsValidator : IValidateOptions<BackofficeTidyOptions>
{
    public ValidateOptionsResult Validate(string? name, BackofficeTidyOptions options)
    {
        var failures = new List<string>();
        for (var i = 0; i < options.Hide.Length; i++)
        {
            var path = $"{BackofficeTidyOptions.SectionName}:Hide:{i}";
            HideRule? rule = options.Hide[i];
            if (rule is null)
            {
                failures.Add($"{path} is empty.");
                continue;
            }

            if (rule.Alias is not null && rule.Preset is not null)
            {
                failures.Add($"{path} sets both Alias and Preset; set exactly one.");
            }
            else if (rule.Alias is null && rule.Preset is null)
            {
                failures.Add($"{path} sets neither Alias nor Preset; set exactly one.");
            }
            else if (rule.Alias is not null && string.IsNullOrWhiteSpace(rule.Alias))
            {
                failures.Add($"{path} has a blank Alias.");
            }
            else if (rule.Preset is not null && !Presets.All.ContainsKey(rule.Preset))
            {
                failures.Add($"{path} names the unknown preset \"{rule.Preset}\". Known presets: {string.Join(", ", Presets.All.Keys.Order(StringComparer.Ordinal))}.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

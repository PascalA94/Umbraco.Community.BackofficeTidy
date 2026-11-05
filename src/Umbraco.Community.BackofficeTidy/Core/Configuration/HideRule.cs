namespace Umbraco.Community.BackofficeTidy.Configuration;

/// <summary>One entry of <c>BackofficeTidy:Hide</c>. Exactly one of <see cref="Alias"/> or <see cref="Preset"/> is set.</summary>
public sealed class HideRule
{
    /// <summary>A backoffice extension alias to hide. Compared exactly, as the extension registry does.</summary>
    public string? Alias { get; set; }

    /// <summary>A named group of aliases from <see cref="Presets"/>, matched ignoring case.</summary>
    public string? Preset { get; set; }

    /// <summary>Host environment names the rule applies to, ignoring case. Empty means every environment.</summary>
    public string[] Environments { get; set; } = [];

    /// <summary>User group aliases, ignoring case. The rule applies to a user in any of them. Empty means every user.</summary>
    public string[] UserGroups { get; set; } = [];
}

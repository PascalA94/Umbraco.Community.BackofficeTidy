namespace Umbraco.Community.BackofficeTidy.Configuration;

/// <summary>Bound from the <c>BackofficeTidy</c> configuration section.</summary>
public sealed class BackofficeTidyOptions
{
    public const string SectionName = "BackofficeTidy";

    public HideRule[] Hide { get; set; } = [];

    /// <summary>Whether the backoffice warns, once per alias, about a hidden alias no extension uses (a typo, or a package that is not installed).</summary>
    public bool WarnUnknownAliases { get; set; } = true;
}

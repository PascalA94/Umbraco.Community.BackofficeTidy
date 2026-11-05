namespace Umbraco.Community.BackofficeTidy.Web.Models;

/// <summary>
/// The extension aliases to hide for the calling user, and the one display flag the client needs. Never carries rules,
/// groups or environment names.
/// </summary>
public sealed class HiddenAliasesResponse
{
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>Whether the client logs a console warning for an alias no extension uses (<c>WarnUnknownAliases</c>).</summary>
    public required bool WarnUnknownAliases { get; init; }
}

namespace Umbraco.Community.BackofficeTidy.Configuration;

/// <summary>
/// Preset names are permanent once released. Their aliases live only in this table, so following a moved alias
/// (Umbraco.AI moves fast) is a one-line patch release; <c>PresetTests</c> pins the verified values.
/// </summary>
public static class Presets
{
    public const string WelcomeDashboard = nameof(WelcomeDashboard);
    public const string Copilot = nameof(Copilot);

    public static IReadOnlyDictionary<string, string[]> All { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [WelcomeDashboard] = ["Umb.Dashboard.UmbracoNews"],
            [Copilot] = ["UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar"],
        };
}

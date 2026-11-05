using Umbraco.Community.BackofficeTidy.Configuration;

namespace Umbraco.Community.BackofficeTidy.Tests;

public class PresetTests
{
    [Fact]
    public void Preset_WelcomeDashboard_IsVerifiedAlias()
        => Assert.Equal(["Umb.Dashboard.UmbracoNews"], Presets.All["WelcomeDashboard"]);

    [Fact]
    public void Preset_Copilot_IsVerifiedAlias()
        => Assert.Equal(["UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar"], Presets.All["Copilot"]);

    [Fact]
    public void Presets_OnlyTheTwoPermanentNames()
        => Assert.Equal(["Copilot", "WelcomeDashboard"], Presets.All.Keys.OrderBy(k => k, StringComparer.Ordinal));

    [Fact]
    public void Presets_AreCaseInsensitiveByName()
        => Assert.True(Presets.All.ContainsKey("welcomedashboard"));
}

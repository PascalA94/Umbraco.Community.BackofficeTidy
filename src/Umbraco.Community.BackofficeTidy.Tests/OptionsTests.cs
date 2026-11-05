using Microsoft.Extensions.Options;
using Umbraco.Community.BackofficeTidy.Configuration;

namespace Umbraco.Community.BackofficeTidy.Tests;

public class OptionsTests
{
    private static ValidateOptionsResult Validate(params HideRule[] rules)
        => new BackofficeTidyOptionsValidator().Validate(null, new BackofficeTidyOptions { Hide = rules });

    [Fact]
    public void Defaults_AreEmptyAndWarn()
    {
        var options = new BackofficeTidyOptions();
        Assert.Empty(options.Hide);
        Assert.True(options.WarnUnknownAliases);
        Assert.True(Validate().Succeeded);
    }

    [Fact]
    public void Validator_AliasAndPreset_FailsNamingIndex()
    {
        var result = Validate(new HideRule { Alias = "ok" }, new HideRule { Alias = "x", Preset = "Copilot" });
        Assert.True(result.Failed);
        Assert.Contains("BackofficeTidy:Hide:1", result.FailureMessage);
    }

    [Fact]
    public void Validator_AliasAndPreset_AtIndexZero_NamesIndexZero()
        => Assert.Contains("BackofficeTidy:Hide:0", Validate(new HideRule { Alias = "x", Preset = "Copilot" }).FailureMessage);

    [Fact]
    public void Validator_NeitherAliasNorPreset_Fails()
        => Assert.Contains("BackofficeTidy:Hide:0", Validate(new HideRule()).FailureMessage);

    [Fact]
    public void Validator_BlankAlias_Fails()
        => Assert.Contains("BackofficeTidy:Hide:0", Validate(new HideRule { Alias = "   " }).FailureMessage);

    [Fact]
    public void Validator_UnknownPreset_ListsPresets()
    {
        var message = Validate(new HideRule { Preset = "Nope" }).FailureMessage;
        Assert.Contains("BackofficeTidy:Hide:0", message);
        Assert.Contains("Copilot", message);
        Assert.Contains("WelcomeDashboard", message);
    }

    [Fact]
    public void Validator_ReportsEveryBadRule()
    {
        var message = Validate(new HideRule(), new HideRule { Alias = "ok" }, new HideRule { Preset = "Nope" }).FailureMessage;
        Assert.Contains("BackofficeTidy:Hide:0", message);
        Assert.DoesNotContain("BackofficeTidy:Hide:1", message);
        Assert.Contains("BackofficeTidy:Hide:2", message);
    }

    [Fact]
    public void Validator_ValidPresetAndAlias_Succeeds()
        => Assert.True(Validate(new HideRule { Preset = "copilot" }, new HideRule { Alias = "Umb.HeaderApp.Search", UserGroups = ["writer"] }).Succeeded);
}

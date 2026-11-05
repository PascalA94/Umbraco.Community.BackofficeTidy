using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Community.BackofficeTidy.Web;

namespace Umbraco.Community.BackofficeTidy.Tests;

/// <summary>
/// Binds the documented appsettings keys through the same registration the composer uses, so renaming the section or
/// a rule property, or dropping validate-on-start, fails here instead of silently hiding nothing.
/// </summary>
public class StartupValidationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddBackofficeTidyOptions(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void StartUp_AliasAndPreset_FailsNamingTheRule()
    {
        using var provider = Build(new()
        {
            ["BackofficeTidy:Hide:0:Alias"] = "x",
            ["BackofficeTidy:Hide:0:Preset"] = "Copilot",
        });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("BackofficeTidy:Hide:0", ex.Message);
    }

    [Fact]
    public void Bind_DocumentedKeys_ReachTheOptions()
    {
        using var provider = Build(new()
        {
            ["BackofficeTidy:WarnUnknownAliases"] = "false",
            ["BackofficeTidy:Hide:0:Preset"] = "Copilot",
            ["BackofficeTidy:Hide:0:Environments:0"] = "Production",
            ["BackofficeTidy:Hide:1:Alias"] = "Umb.HeaderApp.Search",
            ["BackofficeTidy:Hide:1:UserGroups:0"] = "writer",
        });

        provider.GetRequiredService<IStartupValidator>().Validate();
        var options = provider.GetRequiredService<IOptions<Configuration.BackofficeTidyOptions>>().Value;

        Assert.False(options.WarnUnknownAliases);
        Assert.Equal("Copilot", options.Hide[0].Preset);
        Assert.Equal(["Production"], options.Hide[0].Environments);
        Assert.Equal("Umb.HeaderApp.Search", options.Hide[1].Alias);
        Assert.Equal(["writer"], options.Hide[1].UserGroups);
    }

    [Fact]
    public void StartUp_NoSection_Succeeds()
    {
        using var provider = Build([]);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Empty(provider.GetRequiredService<IOptions<Configuration.BackofficeTidyOptions>>().Value.Hide);
    }
}

using Umbraco.Community.BackofficeTidy.Configuration;

namespace Umbraco.Community.BackofficeTidy.Tests;

public class ResolverTests
{
    private static readonly string[] Writer = ["writer"];
    private static readonly string[] Editor = ["editor"];

    private static IReadOnlyList<string> Resolve(IEnumerable<HideRule> rules, string env = "Production", params string[] groups)
        => new HiddenAliasResolver().Resolve(rules, env, groups);

    [Fact]
    public void Groups_AnyMatch()
    {
        HideRule[] rules =
        [
            new() { Alias = "a.writer", UserGroups = Writer },
            new() { Alias = "a.editor", UserGroups = Editor },
        ];
        Assert.Equal(["a.writer"], Resolve(rules, "Production", "writer", "translator"));
        Assert.Equal(["a.editor", "a.writer"], Resolve(rules, "Production", "writer", "editor"));
    }

    [Fact]
    public void Groups_CaseInsensitive()
        => Assert.Equal(["x"], Resolve([new() { Alias = "x", UserGroups = ["Writer"] }], "Production", "WRITER"));

    [Fact]
    public void Environment_CaseInsensitive()
        => Assert.Equal(["x"], Resolve([new() { Alias = "x", Environments = ["production"] }], "Production"));

    [Fact]
    public void Environment_NonMatching_Excluded()
        => Assert.Empty(Resolve([new() { Alias = "x", Environments = ["Production"] }], "Development"));

    [Fact]
    public void Duplicates_Collapsed()
    {
        HideRule[] rules =
        [
            new() { Alias = "dup" },
            new() { Alias = "DUP" },
            new() { Preset = "WelcomeDashboard" },
            new() { Alias = "Umb.Dashboard.UmbracoNews" },
        ];
        Assert.Equal(["DUP", "Umb.Dashboard.UmbracoNews", "dup"], Resolve(rules).OrderBy(a => a, StringComparer.Ordinal));
        Assert.Equal(3, Resolve(rules).Count);
    }

    [Fact]
    public void NoRules_Empty() => Assert.Empty(Resolve([]));

    [Fact]
    public void EmptyLists_MeanAll()
        => Assert.Equal(["x"], Resolve([new() { Alias = "x", Environments = [], UserGroups = [] }], "Anything", "any-group"));

    [Fact]
    public void GroupRule_NoGroupsForUser_NotApplied()
        => Assert.Empty(Resolve([new() { Alias = "x", UserGroups = Writer }], "Production"));

    [Fact]
    public void Preset_Resolves_ToItsAliases()
        => Assert.Equal(["UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar"], Resolve([new() { Preset = "Copilot" }]));

    [Fact]
    public void Preset_NameIsCaseInsensitive()
        => Assert.Equal(["Umb.Dashboard.UmbracoNews"], Resolve([new() { Preset = "welcomedashboard" }]));

    [Fact]
    public void Output_IsSortedOrdinal()
        => Assert.Equal(["B", "a", "b"], Resolve([new() { Alias = "b" }, new() { Alias = "B" }, new() { Alias = "a" }]));

    [Fact]
    public void BlankOrUnknown_RulesAreSkipped_NeverThrow()
        => Assert.Empty(Resolve([new() { Alias = "  " }, new() { Preset = "Nope" }, new()]));
}

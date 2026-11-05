using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.BackofficeTidy.Configuration;

namespace Umbraco.Community.BackofficeTidy.IntegrationTests;

/// <summary>The endpoint over real HTTP, behind the real authentication and authorization pipeline.</summary>
[TestFixture]
public class HiddenAliasesApiTests : ApiTestBase
{
    private const string Route = ManagementApi + "backoffice-tidy/hidden";

    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.Configure<BackofficeTidyOptions>(o => o.Hide =
        [
            new HideRule { Preset = "WelcomeDashboard" },
            new HideRule { Alias = "writers.only", UserGroups = ["writer"] },
            new HideRule { Alias = "editors.only", UserGroups = ["editor"] },
        ]);

    private static async Task<string[]> AliasesAsync(HttpResponseMessage response)
    {
        JsonElement body = await ReadJsonAsync(response);
        return body.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()!).ToArray();
    }

    [Test]
    public async Task Anonymous_Returns401()
        => Assert.That((await CreateAnonymousClient().GetAsync(Route)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

    [Test]
    public async Task Writer_GetsOnlyTheirAliases_WithNoStore()
    {
        IUser writer = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(writer);

        HttpResponseMessage response = await client.GetAsync(Route);

        Assert.That(await AliasesAsync(response), Is.EqualTo(new[] { "Umb.Dashboard.UmbracoNews", "writers.only" }));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
    }

    [Test]
    public async Task Editor_DoesNotSeeTheWritersRule()
    {
        IUser editor = await Users.CreateEditorAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(editor);

        Assert.That(await AliasesAsync(await client.GetAsync(Route)), Is.EqualTo(new[] { "Umb.Dashboard.UmbracoNews", "editors.only" }));
    }

    [Test]
    public async Task Response_CarriesNoRulesGroupsOrEnvironments()
    {
        IUser admin = await Users.CreateAdminAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        JsonElement body = await ReadJsonAsync(await client.GetAsync(Route));

        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "aliases", "warnUnknownAliases" }));
    }
}

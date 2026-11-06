using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.BackofficeTidy.Configuration;
using Umbraco.Community.BackofficeTidy.Web.Controllers;
using Umbraco.Community.BackofficeTidy.Web.Models;

namespace Umbraco.Community.BackofficeTidy.Tests;

public class HiddenAliasesControllerTests
{
    private static readonly HideRule[] Rules =
    [
        new() { Alias = "for.writer", UserGroups = ["writer"] },
        new() { Alias = "for.editor", UserGroups = ["editor"] },
        new() { Alias = "for.everyone" },
        new() { Alias = "prod.only", Environments = ["Production"] },
    ];

    private static IReadOnlyUserGroup Group(string alias)
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns(alias);
        return group;
    }

    private static HiddenAliasesController Create(string[]? groups, string environment = "Development", HideRule[]? rules = null, bool warnUnknownAliases = true)
    {
        var security = Substitute.For<IBackOfficeSecurityAccessor>();
        var backOffice = Substitute.For<IBackOfficeSecurity>();
        security.BackOfficeSecurity.Returns(backOffice);
        backOffice.CurrentUser.Returns((IUser?)null);
        if (groups is not null)
        {
            IReadOnlyUserGroup[] userGroups = groups.Select(Group).ToArray();
            var user = Substitute.For<IUser>();
            user.Groups.Returns(userGroups);
            backOffice.CurrentUser.Returns(user);
        }

        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var options = Substitute.For<IOptionsMonitor<BackofficeTidyOptions>>();
        options.CurrentValue.Returns(new BackofficeTidyOptions { Hide = rules ?? Rules, WarnUnknownAliases = warnUnknownAliases });

        return new HiddenAliasesController(security, options, new HiddenAliasResolver(), env)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [Fact]
    public void Get_ReturnsCallersAliasesOnly()
    {
        var controller = Create(["writer"]);
        var body = Assert.IsType<HiddenAliasesResponse>(Assert.IsType<OkObjectResult>(controller.GetHiddenAliases()).Value);
        Assert.Equal(["for.everyone", "for.writer"], body.Aliases);
        Assert.DoesNotContain("for.editor", body.Aliases);
        Assert.DoesNotContain("prod.only", body.Aliases);
    }

    [Fact]
    public void Get_UsesTheServerEnvironment()
    {
        var body = Assert.IsType<HiddenAliasesResponse>(Assert.IsType<OkObjectResult>(Create(["writer"], "production").GetHiddenAliases()).Value);
        Assert.Contains("prod.only", body.Aliases);
    }

    [Fact]
    public void Get_SetsNoStore()
    {
        var controller = Create(["writer"]);
        controller.GetHiddenAliases();
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Get_NoUser_Returns401()
        => Assert.IsType<UnauthorizedResult>(Create(null).GetHiddenAliases());

    [Fact]
    public void Get_PassesTheWarnUnknownAliasesFlag()
    {
        var body = Assert.IsType<HiddenAliasesResponse>(Assert.IsType<OkObjectResult>(Create(["writer"]).GetHiddenAliases()).Value);
        Assert.True(body.WarnUnknownAliases);
    }

    [Fact]
    public void Get_PassesTheWarnUnknownAliasesFlag_WhenTurnedOff()
    {
        var body = Assert.IsType<HiddenAliasesResponse>(
            Assert.IsType<OkObjectResult>(Create(["writer"], warnUnknownAliases: false).GetHiddenAliases()).Value);
        Assert.False(body.WarnUnknownAliases);
    }

    [Fact]
    public void Get_NoRules_ReturnsEmptyList()
    {
        var body = Assert.IsType<HiddenAliasesResponse>(Assert.IsType<OkObjectResult>(Create(["writer"], rules: []).GetHiddenAliases()).Value);
        Assert.Empty(body.Aliases);
    }
}

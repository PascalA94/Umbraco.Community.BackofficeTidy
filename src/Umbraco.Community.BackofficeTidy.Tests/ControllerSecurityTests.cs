using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Community.BackofficeTidy.Web;

namespace Umbraco.Community.BackofficeTidy.Tests;

/// <summary>S1 [Critical]: the package exposes Management API controllers only, and none is anonymous.</summary>
public class ControllerSecurityTests
{
    private static Assembly PackageAssembly => typeof(BackofficeTidyComposer).Assembly;

    private static List<Type> Controllers()
        => PackageAssembly.GetTypes().Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();

    private static IEnumerable<Type> ChainInPackage(Type type)
    {
        for (Type? t = type; t is not null && t.Assembly == PackageAssembly; t = t.BaseType)
        {
            yield return t;
        }
    }

    [Fact]
    public void AllControllers_AreManagementApi_NoAnonymous()
    {
        List<Type> controllers = Controllers();
        Assert.NotEmpty(controllers);

        foreach (Type controller in controllers)
        {
            Assert.True(typeof(ManagementApiControllerBase).IsAssignableFrom(controller), $"{controller.Name} must inherit ManagementApiControllerBase");
            Assert.EndsWith(
                "/backoffice-tidy",
                controller.GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>(inherit: true)?.Template);
            // Core's base class carries its own [Authorize], so inherited attributes would pass even if our base weakened
            // the policy. Check the ones this package declares itself (the controller and its bases in this assembly).
            AuthorizeAttribute[] ours = ChainInPackage(controller)
                .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: false))
                .ToArray();
            Assert.NotEmpty(ours);
            Assert.All(ours, a => Assert.Equal(Umbraco.Cms.Web.Common.Authorization.AuthorizationPolicies.BackOfficeAccess, a.Policy));
            Assert.Equal("Backoffice Tidy", controller.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ApiExplorerSettingsAttribute>(inherit: true)?.GroupName);
        }
    }

    [Fact]
    public void NothingInTheAssembly_AllowsAnonymous()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (Type type in PackageAssembly.GetTypes())
        {
            Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
            Assert.DoesNotContain(type.GetMethods(all), m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
        }
    }

    [Fact]
    public void OnlyAction_IsAGetNamedHidden()
    {
        MethodInfo[] actions = Controllers()
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .ToArray();
        MethodInfo action = Assert.Single(actions);
        Assert.Equal("GetHiddenAliases", action.Name);
        Assert.Equal("hidden", action.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()?.Template);
    }
}

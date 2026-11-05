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
        => PackageAssembly.GetTypes().Where(t => t.Name.EndsWith("Controller", StringComparison.Ordinal) && !t.IsAbstract).ToList();

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
            Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
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

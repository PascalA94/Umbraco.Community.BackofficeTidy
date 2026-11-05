using System.Text.Json;

namespace Umbraco.Community.BackofficeTidy.Tests;

public class PackageManifestTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [Theory]
    [InlineData("Client", "public", "umbraco-package.json")]
    [InlineData("wwwroot", "umbraco-package.json")]
    public void Manifest_IdentityIsPermanent(params string[] relative)
    {
        var path = RepoFile(new[] { "src", "Umbraco.Community.BackofficeTidy" }.Concat(relative).ToArray());
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        Assert.Equal("Umbraco.Community.BackofficeTidy", root.GetProperty("name").GetString());
        var extensions = root.GetProperty("extensions");
        Assert.Equal(1, extensions.GetArrayLength());
        var bundle = extensions[0];
        Assert.Equal("bundle", bundle.GetProperty("type").GetString());
        Assert.Equal("BackofficeTidy.Bundle", bundle.GetProperty("alias").GetString());
        Assert.Equal("/App_Plugins/BackofficeTidy/index.js", bundle.GetProperty("js").GetString());
    }
}

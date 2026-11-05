using System.Reflection;

namespace Umbraco.Community.BackofficeTidy.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Assembly_TargetsSelectedMajor()
    {
#if UMBRACO_17
        const int expected = 17;
#else
        const int expected = 18;
#endif
        var info = typeof(PackageConstants).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.StartsWith($"{expected}.", info);
    }
}

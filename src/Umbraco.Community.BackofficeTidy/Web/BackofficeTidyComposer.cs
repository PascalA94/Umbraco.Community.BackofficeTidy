using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.BackofficeTidy.Configuration;
using Umbraco.Community.BackofficeTidy.Web.OpenApi;

namespace Umbraco.Community.BackofficeTidy.Web;

/// <summary>The package's only composer.</summary>
public sealed class BackofficeTidyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddBackofficeTidyOptions(builder.Config);

        builder.Services.AddSingleton<HiddenAliasResolver>();
        builder.AddBackofficeTidyOpenApi();
    }
}

internal static class BackofficeTidyServiceCollectionExtensions
{
    /// <summary>Binds and validates the options. Split out of the composer so tests can run the same registration.</summary>
    internal static IServiceCollection AddBackofficeTidyOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // A malformed rule (both Alias and Preset, an unknown preset) stops start-up and names the rule's path.
        services
            .AddOptions<BackofficeTidyOptions>()
            .Bind(configuration.GetSection(BackofficeTidyOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BackofficeTidyOptions>, BackofficeTidyOptionsValidator>());
        return services;
    }
}

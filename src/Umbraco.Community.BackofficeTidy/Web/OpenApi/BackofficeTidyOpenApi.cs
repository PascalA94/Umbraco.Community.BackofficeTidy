#if UMBRACO_17
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
#else
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core;
#endif
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.BackofficeTidy.Web.Controllers;

namespace Umbraco.Community.BackofficeTidy.Web.OpenApi;

/// <summary>
/// The package's own OpenAPI document, used to generate the client. 18 serves it at /umbraco/openapi/backoffice-tidy.json
/// (Microsoft.AspNetCore.OpenApi); 17 at /umbraco/swagger/backoffice-tidy/swagger.json (Swashbuckle, as core's documents are on 17.x).
/// </summary>
internal static class BackofficeTidyOpenApi
{
#if UMBRACO_17
    public static IUmbracoBuilder AddBackofficeTidyOpenApi(this IUmbracoBuilder builder)
    {
        builder.Services.ConfigureOptions<BackofficeTidySwaggerGenOptions>();

        // Core's OperationIdHandler only names operations in the Umbraco.Cms.Api namespace; without this the
        // package's operation gets Swashbuckle's default ID and the generated function name changes.
        builder.Services.AddSingleton<IOperationIdHandler, BackofficeTidyOperationIdHandler>();
        return builder;
    }
#else
    public static IUmbracoBuilder AddBackofficeTidyOpenApi(this IUmbracoBuilder builder)
        => builder.AddBackOfficeOpenApiDocument(
            BackofficeTidyOpenApiNames.DocumentName,
            document => document
                .WithTitle(BackofficeTidyOpenApiNames.GroupName)
                .WithBackOfficeAuthentication()
                .WithJsonOptions(Constants.JsonOptionsNames.BackOffice));
#endif
}

#if UMBRACO_17
internal sealed class BackofficeTidySwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc(
            BackofficeTidyOpenApiNames.DocumentName,
            new OpenApiInfo { Title = BackofficeTidyOpenApiNames.GroupName, Version = "Latest" });
        options.OperationFilter<BackofficeTidySecurityRequirementsOperationFilter>();
    }
}

internal sealed class BackofficeTidySecurityRequirementsOperationFilter : BackOfficeSecurityRequirementsOperationFilterBase
{
    protected override string ApiName => BackofficeTidyOpenApiNames.DocumentName;
}

internal sealed class BackofficeTidyOperationIdHandler : OperationIdHandler
{
    public BackofficeTidyOperationIdHandler(IOptions<Asp.Versioning.ApiVersioningOptions> apiVersioningOptions)
        : base(apiVersioningOptions)
    {
    }

    protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
        => typeof(BackofficeTidyControllerBase).IsAssignableFrom(controllerActionDescriptor.ControllerTypeInfo);
}
#endif

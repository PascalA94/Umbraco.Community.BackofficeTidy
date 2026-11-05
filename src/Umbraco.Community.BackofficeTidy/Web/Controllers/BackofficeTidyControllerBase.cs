using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Umbraco.Community.BackofficeTidy.Web.Controllers;

/// <summary>
/// S1 [Critical]: every package route is a Management API route behind the backoffice access policy, never anonymous.
/// The route segment, group name and document name are permanent once released.
/// </summary>
[VersionedApiBackOfficeRoute(RouteSegment)]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[ApiExplorerSettings(GroupName = BackofficeTidyOpenApiNames.GroupName)]
[MapToApi(BackofficeTidyOpenApiNames.DocumentName)]
public abstract class BackofficeTidyControllerBase : ManagementApiControllerBase
{
    public const string RouteSegment = "backoffice-tidy";
}

public static class BackofficeTidyOpenApiNames
{
    public const string DocumentName = "backoffice-tidy";

    public const string GroupName = "Backoffice Tidy";
}

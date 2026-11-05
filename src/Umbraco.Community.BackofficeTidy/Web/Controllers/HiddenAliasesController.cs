using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.BackofficeTidy.Configuration;
using Umbraco.Community.BackofficeTidy.Web.Models;

namespace Umbraco.Community.BackofficeTidy.Web.Controllers;

[ApiVersion("1.0")]
public sealed class HiddenAliasesController : BackofficeTidyControllerBase
{
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IOptionsMonitor<BackofficeTidyOptions> _options;
    private readonly HiddenAliasResolver _resolver;
    private readonly IHostEnvironment _environment;

    public HiddenAliasesController(
        IBackOfficeSecurityAccessor security,
        IOptionsMonitor<BackofficeTidyOptions> options,
        HiddenAliasResolver resolver,
        IHostEnvironment environment)
    {
        _security = security;
        _options = options;
        _resolver = resolver;
        _environment = environment;
    }

    [HttpGet("hidden")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(HiddenAliasesResponse), StatusCodes.Status200OK)]
    [EndpointSummary("Gets the extension aliases to hide for the current user.")]
    public IActionResult GetHiddenAliases()
    {
        IUser? user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return Unauthorized();
        }

        // The answer differs per caller, so no shared cache may keep it.
        Response.Headers.CacheControl = "no-store";

        var groups = user.Groups.Select(g => g.Alias).ToArray();
        BackofficeTidyOptions options = _options.CurrentValue;
        IReadOnlyList<string> aliases = _resolver.Resolve(options.Hide, _environment.EnvironmentName, groups);
        return Ok(new HiddenAliasesResponse { Aliases = aliases, WarnUnknownAliases = options.WarnUnknownAliases });
    }
}

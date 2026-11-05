using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Umbraco.Community.BackofficeTidy.DemoSite;

/// <summary>
/// Demo only: creates "Wren Writer" (writer group) and "Ed Editor" (editor group) once the site has installed.
/// Passwords come from configuration (user-secrets locally, env vars in CI), never from source. A user whose password
/// is not configured is skipped.
/// </summary>
public sealed class DemoUsers(
    IUserService userService,
    IUserGroupService userGroupService,
    ICoreBackOfficeUserManager userManager,
    IConfiguration configuration,
    ILogger<DemoUsers> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        await EnsureAsync("Wren Writer", "writer@example.test", "writer", configuration["Demo:WriterPassword"]);
        await EnsureAsync("Ed Editor", "editor@example.test", "editor", configuration["Demo:EditorPassword"]);
    }

    private async Task EnsureAsync(string name, string email, string groupAlias, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            logger.LogInformation("Demo user {Email} skipped: no password configured.", email);
            return;
        }

        if (userService.GetByEmail(email) is not null)
        {
            return;
        }

        IUserGroup group = await userGroupService.GetAsync(groupAlias)
            ?? throw new InvalidOperationException($"No user group '{groupAlias}'.");
        var created = await userService.CreateAsync(
            Constants.Security.SuperUserKey,
            new UserCreateModel { Email = email, UserName = email, Name = name, UserGroupKeys = new HashSet<Guid> { group.Key } },
            approveUser: true);
        if (!created.Success || created.Result.CreatedUser is not { } user)
        {
            logger.LogWarning("Demo user {Email} was not created: {Status}", email, created.Status);
            return;
        }

        var reset = await userManager.GeneratePasswordResetTokenAsync(user);
        var changed = await userService.ChangePasswordAsync(user.Key, new ChangeUserPasswordModel
        {
            NewPassword = password,
            ResetPasswordToken = reset.Result!.ToUrlBase64(),
            UserKey = user.Key,
        });
        if (!changed.Success)
        {
            logger.LogWarning("Demo user {Email} has no password: {Status}", email, changed.Status);
        }
    }
}

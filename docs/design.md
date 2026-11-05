# Design: Backoffice Tidy

| | |
|---|---|
| Package | `Umbraco.Community.BackofficeTidy` |
| Owner | Pascal Meyer (`PascalA94`) |
| Size | S, about 4-7 h agent time (3-5 h plus 1-2 h for the second line) |
| Lines | **Both from the start** (Pascal, 2026-11-04: client sites are on v17 LTS): `17.x` on CMS `[17.8.0, 18.0.0)` and `18.x` on `[18.3.0, 19.0.0)`, one codebase, both packed from one commit |
| Status | Draft design, 2026-11-04. Nothing built |
| Inputs | the package scope, the portfolio notes, the Umbraco pitfalls reference and a sibling package (house style for the backoffice client and Management API) |

Tags: **[verified]** read this session at the linked source; **[unverified]** recalled or inferred, check while building; **[DATED]** true on 2026-11-04 and likely to move.

---

## 0. What was verified (2026-11-04)

All [verified], read with `gh api` at Umbraco-CMS `release-18.3.0`, and the same lines checked at `release-17.8.0`.

| Fact | Source |
|---|---|
| `UmbExtensionRegistry.exclude(alias)` adds the alias to an exclusion list and filters it out of the current extensions. `register` and `registerMany` reject excluded aliases later, so it also covers packages that load **after** us | [extension.registry.ts L180-260](https://github.com/umbraco/Umbraco-CMS/blob/release-18.3.0/src/Umbraco.Web.UI.Client/src/libs/extension-api/registry/extension.registry.ts) |
| `unregister(alias)` exists, but core's own doc comment says "it is more secure to exclude it, only unregister extensions that you provided" | same file, L264-290 |
| `appendCondition(alias, condition)` stores conditions for aliases not yet registered and applies them on registration | same file, L612-650 |
| `backofficeEntryPoint` loads "after the Backoffice has been loaded and authentication has been done" and gets `onInit(host, extensionRegistry)`. `entryPoint` is deprecated in favour of it. Both exist on 17.8.0 | [backoffice-entry-point.extension.ts](https://github.com/umbraco/Umbraco-CMS/blob/release-18.3.0/src/Umbraco.Web.UI.Client/src/packages/core/extension-registry/extensions/backoffice-entry-point.extension.ts), [entry-point.extension.ts](https://github.com/umbraco/Umbraco-CMS/blob/release-18.3.0/src/Umbraco.Web.UI.Client/src/packages/core/extension-registry/extensions/entry-point.extension.ts) |
| The entry-point initializer has `unloadExtension`, which calls the module's `onUnload` | [backoffice-entry-point-extension-initializer.ts](https://github.com/umbraco/Umbraco-CMS/blob/release-18.3.0/src/Umbraco.Web.UI.Client/src/packages/core/extension-registry/initializers/backoffice-entry-point-extension-initializer.ts) |
| The core condition `Umb.Condition.CurrentUser.GroupId` (`match`, `oneOf`, `allOf`, `noneOf`) takes group **GUIDs** | [group-id constants and config](https://github.com/umbraco/Umbraco-CMS/blob/release-18.3.0/src/Umbraco.Web.UI.Client/src/packages/user/current-user/conditions/group-id/group-id.condition-config.ts) |
| The Welcome dashboard alias is `Umb.Dashboard.UmbracoNews` (17.8.0 and 18.3.0) | [umbraco-news/umbraco-package.ts](https://github.com/umbraco/Umbraco-CMS/blob/release-18.3.0/src/Umbraco.Web.UI.Client/src/packages/umbraco-news/umbraco-package.ts) |
| The Copilot sidebar and floating button come from one `backofficeEntryPoint`, `UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar`, which has an `onUnload` that removes both. Same alias on `v17/dev` and `v18/dev` [DATED: Umbraco.AI moves fast] | [Umbraco.AI v18/dev sidebar/manifests.ts](https://github.com/umbraco/Umbraco.AI/blob/v18/dev/Umbraco.AI.Agent.Copilot/src/Umbraco.AI.Agent.Copilot/Client/src/copilot/components/sidebar/manifests.ts), [entry-point.ts](https://github.com/umbraco/Umbraco.AI/blob/v18/dev/Umbraco.AI.Agent.Copilot/src/Umbraco.AI.Agent.Copilot/Client/src/copilot/components/sidebar/entry-point.ts) |

Not verified, so spiked in Task 2:
- The initializer base calls `unloadExtension` when an already-loaded entry point is excluded [unverified]. If not, the Copilot preset needs a page reload, or must win the load race.
- How visible a dashboard flash is before exclusion [unverified].

---

## 1. Intent and goals

Hide backoffice extensions by alias from appsettings, scoped per environment and per user group, with no hand-written `umbraco-package.json`.

**Goals**
- Rules in `BackofficeTidy:Hide`: an alias or a preset, plus optional environments and user groups.
- Presets: `WelcomeDashboard` and `Copilot`.
- The server works out which rules apply to the current user. The client excludes those aliases.
- An unknown alias gives one console warning, to help with typos.

**Non-goals (1.0)**
- A settings UI.
- Re-ordering or renaming extensions.
- Hiding parts of an extension (for example one tab of a workspace).
- Hiding properties on documents.

**Anti-goals**
- **Never pretend hiding is access control.** A hidden section, dashboard or Copilot button leaves every API behind it working for anyone whose permissions allow it.
- Never break the backoffice. If the rules can't be fetched, hide nothing and warn.

---

## 2. Constraints

- **Both lines from the start**, using the Teams and Jira pattern:
  - `eng/lines.txt` = `17` and `18`, with the `UmbracoMajor` switch and `packages.$(UmbracoMajor).lock.json`;
  - one `release/*` tag packs `17.x` and `18.x`;
  - four CI legs.
- **One client bundle serves both lines.** `@umbraco-cms/backoffice` is a Vite external, so the bundle holds only bare imports (`@umbraco-cms/backoffice/extension-api` and similar) that the host's import map resolves at runtime. Every API used is on both tags (section 0).
  - The committed `wwwroot/` is built against `^17.8.0`, the lower line.
  - A CI job also type-checks `Client/src` against `^18.3.0` (`npm i --no-save` in a temp copy, `tsc --noEmit`), so an 18-only break shows on the PR.
  - If a line ever needs different code, split it into two bundles then, not before.
- A Razor class library on `Umbraco.Cms.Api.Management` at the line's range. Client: TypeScript, Lit and Vite, with `@umbraco-cms/backoffice` as a `devDependency` and a Vite external.
- Pitfalls:
  - S1 [Critical]: the controller inherits `ManagementApiControllerBase`, with `[VersionedApiBackOfficeRoute("backoffice-tidy")]` and authenticated by default. Never `UmbracoApiController` or `[AllowAnonymous]`.
  - P2 [Critical]: no statics.
  - S3 [Critical]: no user data rendered.
- Portfolio CI rules (README) win.

---

## 3. MVP spec

### 3.1 Config keys

```json
"BackofficeTidy": {
  "Hide": [
    { "Preset": "WelcomeDashboard" },
    { "Preset": "Copilot", "Environments": ["Production"] },
    { "Alias": "Umb.HeaderApp.Search", "UserGroups": ["writer", "translator"] }
  ],
  "WarnUnknownAliases": true
}
```

- A rule has exactly one of `Alias` or `Preset`. Anything else is a start-up error naming the rule index.
- `Environments` is matched against `IHostEnvironment.EnvironmentName`, ignoring case. Empty means all environments.
- `UserGroups` are group aliases, ignoring case. Empty means every user. A rule applies when the user is in **any** listed group (Q1).
- Presets:
  - `WelcomeDashboard` is `Umb.Dashboard.UmbracoNews`.
  - `Copilot` is `UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar`. Its aliases live in one C# table with a test, so updating them is a one-line patch release.

### 3.2 Behaviour

- **Server:** `GET /umbraco/management/api/v1/backoffice-tidy/hidden` returns `{ "aliases": ["..."] }` for the **calling user only**. It resolves the user's groups through `IBackOfficeSecurityAccessor`, and the environment on the server.
  - It never returns the rule list, other groups' rules or environment names.
  - It sends `Cache-Control: no-store`.
- **Client:** one `backofficeEntryPoint` (`BackofficeTidy.EntryPoint`). `onInit`:
  - fetches the endpoint with the backoffice's authenticated client;
  - calls `extensionRegistry.exclude(alias)` for each alias;
  - if `WarnUnknownAliases` is on, after the first idle tick it does one `console.warn` per alias that `isRegistered` doesn't know: `BackofficeTidy: no extension with alias "<alias>" (typo, or the package is not installed).` The alias comes from config, never user content.
- **On fetch failure:** no exclusions, and `console.warn("BackofficeTidy: could not load rules; nothing hidden.")`.

### 3.3 Acceptance criteria

- **Given** `{ "Preset": "WelcomeDashboard" }`, **when** any user opens Content, **then** no "Welcome" dashboard tab shows.
- **Given** `{ "Preset": "Copilot", "Environments": ["Production"] }` and the site runs as `Production`, **when** a user signs in, **then** no Copilot button or sidebar shows. **Given** `Development`, **then** Copilot shows.
- **Given** a rule for `Umb.HeaderApp.Search` with `UserGroups: ["writer"]`, **when** a writer signs in, **then** the search header app is gone. **When** an admin signs in, **then** it is there.
- **Given** a rule with `Alias: "Umb.Dashbaord.Typo"`, **when** the backoffice loads, **then** the console shows one warning naming that alias **and** nothing else changes.
- **Given** the endpoint returns 500, **when** the backoffice loads, **then** nothing is hidden **and** one warning shows.
- **Given** an anonymous request to the endpoint, **then** 401.
- **Given** a writer, **when** they call the endpoint, **then** the response lists only the aliases hidden for them.
- **Given** `{ "Alias": "x", "Preset": "Copilot" }`, **when** the site starts, **then** it fails naming `BackofficeTidy:Hide:0`.

---

## 4. Technical design

```
src/Umbraco.Community.BackofficeTidy/
  BackofficeTidyComposer.cs        // options + ValidateOnStart, rule evaluator, OpenAPI doc
  Configuration/{BackofficeTidyOptions,HideRule,Presets}.cs
  HiddenAliasResolver.cs           // pure: (rules, environment, groupAliases) -> IReadOnlyList<string>
  Controllers/HiddenAliasesController.cs  // ManagementApiControllerBase, GET "hidden"
  Client/src/entry-point.ts        // onInit: fetch + exclude + warn
  Client/src/api/                  // generated with @hey-api/openapi-ts, same as 01
  Client/public/umbraco-package.json  // one bundle, one backofficeEntryPoint
```

- **Why the server decides.** The core group condition takes GUIDs, which differ per site, and the environment is only known on the server. One pure resolver keeps the logic testable, and the client is about 30 lines.
- **Why `exclude`, not `unregister` or conditions.** It is core's recommended route for other packages' extensions, and it also blocks later registration. Conditions would need GUIDs and keep the extension "registered", so it could still show elsewhere.
- **Load order.** Our entry point runs after auth, like Copilot's, so either may run first. If Copilot wins, `exclude` removes it, and the initializer should call its `onUnload` (Task 2 spike). The fallback is documented: "Copilot may flash for a moment on first load."
- OpenAPI group `Backoffice Tidy`, route `backoffice-tidy`, aliases prefixed `BackofficeTidy.`. All of these are permanent once released.

---

## 5. Security notes

- **Hiding is not access control.** This goes first in the README, in bold:
  - A hidden section is still reachable through its APIs, and by URL if the user has section access.
  - A hidden Copilot leaves the Umbraco.AI APIs callable.
  - Hidden dashboards still serve their data endpoints.
  - Use user-group permissions (and Umbraco.AI's own settings) to block access.
- **Endpoint exposure:** authenticated backoffice users only (S1), returning only the caller's aliases. Alias names are public anyway (they ship in the backoffice JS), so the residual risk is low. Even so, environment names and other groups' rules are never returned.
- **No user input is rendered.** Warnings use config values only, and the client has no `innerHTML` or `unsafeHTML` (a CI grep like 01's `check-xss.sh`).
- **Supply chain:** the npm lock file is committed, `npm ci --ignore-scripts`, the backoffice package is an external, and actions are SHA-pinned.

---

## 6. Test strategy

- **Unit (xUnit):**
  - a `HiddenAliasResolver` theory (environment and group combinations, case, presets, empty lists);
  - an options validator;
  - a preset-table pin test (the exact verified aliases).
- **API tests:** the controller reflection check (S1: base class, route, no `AllowAnonymous`), and a resolver call through a stubbed `IBackOfficeSecurityAccessor`. A full `Umbraco.Cms.Tests.Integration` API test only if it is cheap on both lines (01 has the pattern).
- **Client (Vitest):** `entry-point` with a fake registry and fetch: aliases excluded, a fetch failure gives no exclusions and one warning, unknown aliases give one warning each.
- **Both lines:** the .NET suites with `-p:UmbracoMajor=17` and `18`. The client is built and tested against `^17.8.0` and type-checked against `^18.3.0`.
- **Manual (last plan task), on a 17.8 and an 18.3 demo site:** two user groups with different rules, the Welcome preset, and Copilot if Umbraco.AI installs on that line.

---

## 7. Release

- Pascal reserves the NuGet ID with an empty `0.0.1-alpha`, then unlists it.
- `17.0.0-alpha.1` and `18.0.0-alpha.1` from one tag (`release/0.0-alpha.1`), then `17.0.0` and `18.0.0`.
- NuGet and Marketplace tags: `umbraco`, `umbraco-marketplace`, `backoffice`, `dashboard`, `copilot`, `ai`, `user-groups`, `tidy`.
- Repo `PascalA94/Umbraco.Community.BackofficeTidy`, **public at scaffold**, with CodeQL, a branch ruleset, gitleaks, and secret scanning with push protection. `docs/` holds `design.md`, `plan.md` and `spikes.md` only.
- Forum replies on threads 8003 and 8679, and a comment on Umbraco.AI discussion 322, once `alpha.1` is out. Each says plainly that it hides UI and isn't a permission.

---

## 8. Open questions for Pascal

| # | Question | Default |
|---|---|---|
| Q1 | Do rules need an "everyone except these groups" form (`ExceptUserGroups`)? | **No for 1.0.** List the groups to hide for. Add `ExceptUserGroups` if asked |
| Q2 | The Copilot preset follows a moving Umbraco.AI alias. Ship it in 1.0? | **Yes**, pinned to the verified alias, with a test and a README line ("if Copilot still shows, add its alias by hand") |
| Q3 | Warn about unknown aliases in the browser console only, or also in the server log? | **Console only.** The server can't know client aliases |

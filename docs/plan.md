# Backoffice Tidy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Execution is **native, plus one final whole-branch `qa-reviewer` review** (Task 8). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Umbraco.Community.BackofficeTidy` `17.0.0-alpha.1` and `18.0.0-alpha.1` from one commit. Both hide backoffice extensions by alias from appsettings, per environment and user group.

**Architecture:** A Razor class library. A pure `HiddenAliasResolver` turns the configured rules, the environment and the caller's group aliases into a list of aliases. One Management API GET returns that list for the current user. One `backofficeEntryPoint` fetches it and calls `extensionRegistry.exclude(alias)` for each. One client bundle (backoffice imports are externals) serves both lines.

**Tech Stack:** .NET 10, `Umbraco.Cms.Api.Management` `[17.8.0, 18.0.0)` and `[18.3.0, 19.0.0)`, TypeScript, Lit, Vite, `@umbraco-cms/backoffice` `^17.8.0` (type-checked against `^18.3.0`), `@hey-api/openapi-ts`, Vitest, xUnit, NSubstitute, GitHub Actions, gitleaks, osv-scanner, CodeQL.

**Spec:** `docs/design.md`. Portfolio rules and the CI and GitHub rules live in Pascal's private package workspace and win over this plan's CI tasks. Client and OpenAPI patterns follow `Umbraco.Community.ArchivedUsers` (read only).

**Repo:** `PascalA94/Umbraco.Community.BackofficeTidy`. `SRC` = `src/Umbraco.Community.BackofficeTidy`, `TESTS` = `src/Umbraco.Community.BackofficeTidy.Tests`, `CLIENT` = `SRC/Client`.

## Global Constraints

- **Both lines from the start** (Pascal, 2026-11-04). `net10.0`, `eng/lines.txt` = `17` and `18`.
  - `UmbracoMajor` `17` gives `[17.8.0, 18.0.0)`; `18` (the default) gives `[18.3.0, 19.0.0)`.
  - `NuGetLockFilePath` = `packages.$(UmbracoMajor).lock.json`. Version is `$(UmbracoMajor).$(PackageMinor).$(PackagePatch)`, and a `release/*` tag packs both.
  - `scripts/test.sh` loops over the lines. Expected, once per line: `Passed!  - Failed:     0`.
- **Permanent names:**
  - NuGet ID `Umbraco.Community.BackofficeTidy`; route `backoffice-tidy`; action `hidden`; OpenAPI group `Backoffice Tidy`;
  - client aliases `BackofficeTidy.Bundle` and `BackofficeTidy.EntryPoint`;
  - preset names `WelcomeDashboard` (`Umb.Dashboard.UmbracoNews`) and `Copilot` (`UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar`).
- **Config section `BackofficeTidy`**, with keys from spec 3.1, verbatim. A rule has exactly one of `Alias` or `Preset`.
- **S1 [Critical]:** the controller is `ManagementApiControllerBase` with `[VersionedApiBackOfficeRoute("backoffice-tidy")]` and `[ApiExplorerSettings(GroupName = "Backoffice Tidy")]`. Never `[AllowAnonymous]`. The response is the caller's aliases only, with `Cache-Control: no-store`.
- **Client:**
  - `@umbraco-cms/backoffice` is a `devDependency` and a Vite external; `npm ci --ignore-scripts`; the committed `wwwroot/` must match a fresh build (CI drift check);
  - no `innerHTML`, `unsafeHTML` or `unsafeStatic` (CI grep);
  - fetch failure means no exclusions plus one warning, with the copy from spec 3.2.
- **Hiding is not access control.** The README says so first (spec 5).
- **CI** (README rules):
  - a four-leg .NET matrix (lines 17 and 18 × floor and latest);
  - a `client` job (build, Vitest, drift check, XSS grep, `tsc --noEmit` against `@umbraco-cms/backoffice@^18.3.0`);
  - gitleaks per PR commit, osv-scanner with `osv-scanner.toml`, check-vulnerable;
  - `release-checks.yml` is `workflow_dispatch` only; no NuGet cache in `publish.yml`; Dependabot `open-pull-requests-limit: 0`; SHA-pinned actions.
- **CI and GitHub rules (Pascal, 2026-11-04):** the portfolio README.md section "Build approach → CI and GitHub rules" overrides this plan's CI and GitHub tasks: public repo before the first CI push, fast PR checks only, e2e and SQL Server in a manual release-checks.yml, no credentials in workflows (random per-run passwords), e2e locators scoped to visible elements, and the first GitHub CI run planned as its own step before any release tag.
- **Claude never commits or pushes.** Every commit is a "Checkpoint (Pascal commits)" with no AI attribution.

## Review Focus

1. **A writer in two groups**, one with a rule and one without, gets the rule (any-match). Test: Task 3 `Groups_AnyMatch`.
2. **Environment names in a different case** (`production` against `Production`) still match. Test: Task 3 `Environment_CaseInsensitive`.
3. **The same alias in two rules** is returned once. Test: Task 3 `Duplicates_Collapsed`.
4. **Copilot loaded before our entry point:** it disappears after exclusion, at worst after a flash. Test: Task 5 `Exclude_AfterRegister_Removes` (fake registry); manual in Task 9.
5. **An empty or missing `Hide` section:** the endpoint returns `{"aliases":[]}`, and the client calls `exclude` 0 times and gives no warning. Test: Task 3 `NoRules_Empty` and Task 5 `EmptyList_NoWarnings`.

---

### Task 1: Clone and scaffold, with CI and gitleaks from day one

**Files:** `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`, `.gitattributes`, `LICENSE` (MIT, Pascal Meyer), `SECURITY.md`, `src/Umbraco.Community.BackofficeTidy.slnx`, `SRC/*.csproj`, `TESTS/*.csproj`, `TESTS/{ScaffoldTests,PackageManifestTests}.cs`, `tests/e2e/` (Playwright smoke, filled in Task 5), `CLIENT/{package.json,package-lock.json,.nvmrc,tsconfig.json,vite.config.ts,public/umbraco-package.json,src/index.ts}`, `eng/lines.txt`, `scripts/{test,pack,check-xss}.sh`, `osv-scanner.toml`, `.github/workflows/{build,release-checks}.yml`, `.github/dependabot.yml`, `docs/{design,plan,spikes}.md`, `demo/DemoSite.V17/`, `demo/DemoSite.V18/`

- [ ] **Step 1 (Pascal):** create the empty **public** repo `PascalA94/Umbraco.Community.BackofficeTidy` (no README, no licence).
  - Set About: description, NuGet link, topics `umbraco`, `umbracopackage`, `dotnet`, `backoffice`, `copilot`.
  - Turn on secret scanning with push protection, CodeQL default setup (C# and JS/TS), and squash-merge only.
- [x] **Step 2:** `gh repo clone PascalA94/Umbraco.Community.BackofficeTidy`. Confirm `git config user.email` is `5427750+PascalA94@users.noreply.github.com`. Copy only `design.md` and `plan.md` into `docs/`, and start `docs/spikes.md` with spec section 0's two [unverified] items. Expected: `ls docs` shows `design.md plan.md spikes.md`.
- [x] **Step 3:** `Directory.Build.props` with the `UmbracoMajor` switch (constants, range, lock-file path, version prefix, `UmbracoFloat`), modelled on Jira's `AutomateMajor`. The RCL has `StaticWebAssetBasePath` `App_Plugins/BackofficeTidy`.
- [x] **Step 4:** client scaffold, copied from Archived Users and renamed:
  - `umbraco-package.json` has one `bundle` extension, `BackofficeTidy.Bundle`, with `js` set to `/App_Plugins/BackofficeTidy/index.js`;
  - `index.ts` exports `manifests` containing one `backofficeEntryPoint`, `BackofficeTidy.EntryPoint`, with a no-op `onInit`;
  - `package.json` has `@umbraco-cms/backoffice ^17.8.0` under `devDependencies`, `vitest`, `happy-dom` and `"test": "vitest run"`;
  - Vite externals: `[/^@umbraco-cms\/backoffice/]`.
- [x] **Step 5:** write `ScaffoldTests.Assembly_TargetsSelectedMajor` and `PackageManifestTests.Manifest_IdentityIsPermanent` (name, the one bundle, the alias, the js path). Run `./scripts/test.sh`. Expected: FAIL. Run `cd CLIENT && npm install && npm run build`, add the metadata, then re-run. Expected: `Passed!` twice, plus `wwwroot/index.js`.
- [x] **Step 6:** two demo sites from `Umbraco.Templates::17.8.0` and `::18.3.0`, SQLite.
  - Set `UmbracoUseAppLocalIcu=false`.
  - Unattended admin, password in `dotnet user-secrets`.
  - A `ProjectReference` with the matching `UmbracoMajor`.
  - Users "Wren Writer" (`writer`) and "Ed Editor" (`editor`).
- [x] **Step 7:** `build.yml`:
  - `permissions: {}`;
  - jobs `build` (four-leg matrix), `client` (per Global Constraints), `secrets` and `deps`;
  - NuGet cache on.
  - `release-checks.yml`: `workflow_dispatch` only (no `pull_request`, no schedule; README rules 3, 12, 13).
    - Each line's demo site boots on HTTPS (`dotnet dev-certs https`, `--urls https://localhost:<port>`, readiness through `curl -sk`) against SQL Server started with `docker run` in a step, not a `services:` block.
    - The SQL Server and demo-login passwords come from `openssl rand -base64 24` per run, are masked with `::add-mask::` and passed through `$GITHUB_ENV`. No literal credential is in any workflow.
    - A Playwright smoke per line (`tests/e2e`, `workers: 1`): signed in as a random-password writer, the Welcome tab and search header app are absent and the editor still has search. Locators are scoped to their container and visible-only (`.filter({ visible: true })`, never `.first()` on a page-wide locator; README rule 11).
    - NuGet cache on.
    - Run `actionlint`. Expected: no output.
- [x] **Step 8:** `gitleaks dir . --redact` shows no leaks. On each demo site, `/App_Plugins/BackofficeTidy/index.js` returns 200 and the console has no errors.
- [ ] **Checkpoint (Pascal commits):** `chore: scaffold 17 and 18 lines, client, demo sites and CI`. Push a branch, open a PR, confirm the checks are green, then add the ruleset requiring `build`, `client`, `secrets` and `deps`.

### Task 2: Spike exclude timing on both lines

**Files:** `docs/spikes.md` only. A temporary `onInit` in `CLIENT/src/index.ts` is reverted at the end of the task.

- [x] **Step 1:** on each demo site, a temporary `onInit` calls `extensionRegistry.exclude('Umb.Dashboard.UmbracoNews')` after a 2 s `setTimeout`. Record whether the Welcome tab disappears without a reload, and roughly how long it flashes when excluded with no delay.
- [x] **Step 2:** install Umbraco.AI with Copilot on the 18 site (and the 17 site, if a 17.x package exists [DATED]). Exclude `UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar` after 2 s. Record whether the sidebar and button are removed (that is, `onUnload` ran).
- [x] **Step 3:** decide. If `onUnload` doesn't run on exclusion, the README says "Copilot hides from the next page load" and the Copilot preset stays. Record the decision per line.
- [x] **Step 4:** revert the temporary code. `git status` shows only `docs/spikes.md` changed.
- [ ] **Checkpoint (Pascal commits):** `docs: exclude timing spike on 17 and 18`

### Task 3: Options, presets and the resolver

**Files:** `SRC/Configuration/{BackofficeTidyOptions,HideRule,Presets,BackofficeTidyOptionsValidator}.cs`, `SRC/HiddenAliasResolver.cs`, `TESTS/{ResolverTests,OptionsTests,PresetTests}.cs`

**Interfaces:**
- Produces `HideRule { string? Alias; string? Preset; string[] Environments = []; string[] UserGroups = [] }` and `BackofficeTidyOptions { HideRule[] Hide = []; bool WarnUnknownAliases = true }`.
- `static IReadOnlyDictionary<string, string[]> Presets.All`.
- `IReadOnlyList<string> HiddenAliasResolver.Resolve(IEnumerable<HideRule> rules, string environmentName, IReadOnlyCollection<string> groupAliases)`.

- [ ] **Step 1:** write the failing tests:
  - `Preset_WelcomeDashboard_IsVerifiedAlias` (exact string `Umb.Dashboard.UmbracoNews`), `Preset_Copilot_IsVerifiedAlias`;
  - `Groups_AnyMatch`, `Environment_CaseInsensitive`, `Duplicates_Collapsed`, `NoRules_Empty`, `EmptyLists_MeanAll`;
  - `Validator_AliasAndPreset_FailsNamingIndex` (message contains `BackofficeTidy:Hide:0`), `Validator_UnknownPreset_ListsPresets`.
- [ ] **Step 2:** run `./scripts/test.sh`. Expected: FAIL.
- [ ] **Step 3:** implement. The resolver is a pure function, and its output is sorted ordinal for stable tests.
- [ ] **Step 4:** run `./scripts/test.sh`. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: hide rules, presets and resolver`

### Task 4: Management API endpoint

**Files:** `SRC/Controllers/HiddenAliasesController.cs`, `SRC/Models/HiddenAliasesResponse.cs`, `SRC/BackofficeTidyComposer.cs`, `SRC/OpenApi/*` (as Archived Users), `TESTS/{ControllerSecurityTests,HiddenAliasesControllerTests}.cs`

**Interfaces:**
- Consumes `HiddenAliasResolver.Resolve`.
- Produces `GET /umbraco/management/api/v1/backoffice-tidy/hidden`, returning `HiddenAliasesResponse { IReadOnlyList<string> Aliases }`, operation id `GetHiddenAliases`.

- [ ] **Step 1:** write the failing tests:
  - `ControllerSecurityTests.AllControllers_AreManagementApi_NoAnonymous` (reflection: base type, route attribute, no `AllowAnonymous` anywhere in the assembly);
  - `Get_ReturnsCallersAliasesOnly` (stubbed `IBackOfficeSecurityAccessor` with a writer; the response has the writer's aliases and none from an editor-only rule);
  - `Get_SetsNoStore`;
  - `Get_NoUser_Returns401`.
- [ ] **Step 2:** run `./scripts/test.sh`. Expected: FAIL.
- [ ] **Step 3:** implement. Groups come from `CurrentUser.Groups` aliases, and the environment from `IHostEnvironment`. The composer binds options with `ValidateOnStart` and registers the OpenAPI document, the same way as Archived Users. Wire the OpenAPI doc per line, with `#if UMBRACO_17` only if 17.8's OpenAPI registration differs (record in `docs/spikes.md`).
- [ ] **Step 4:** run `./scripts/test.sh`. Expected: PASS on both lines. On each demo site, `GET .../backoffice-tidy/hidden` without a token returns 401.
- [ ] **Checkpoint (Pascal commits):** `feat: hidden-aliases Management API endpoint`

### Task 5: Client entry point

**Files:** `CLIENT/src/{index.ts,entry-point.ts}`, `CLIENT/src/api/*` (generated), `CLIENT/src/entry-point.test.ts`, `wwwroot/*` (built)

**Interfaces:**
- Consumes `GetHiddenAliases` from the generated client.
- Produces `onInit: UmbEntryPointOnInit` and `applyHiddenAliases(registry: Pick<UmbExtensionRegistry, 'exclude' | 'isRegistered'>, aliases: string[], warnUnknown: boolean, warn: (m: string) => void): void`.

- [ ] **Step 1:** generate the client (`npm run generate:api` against a demo site's OpenAPI JSON, same as Archived Users).
- [ ] **Step 2:** write the failing Vitest tests:
  - `excludesEachAlias`;
  - `Exclude_AfterRegister_Removes` (a fake registry backed by core's real `UmbExtensionRegistry` from `@umbraco-cms/backoffice/extension-api`, if it imports under happy-dom; otherwise a fake);
  - `fetchFailure_noExclusions_oneWarning` (exact spec 3.2 copy);
  - `unknownAlias_warnsOnceWithAlias`;
  - `EmptyList_NoWarnings`.
- [ ] **Step 3:** run `npm test`. Expected: FAIL.
- [ ] **Step 4:** implement. `onInit` gets the auth config from `UMB_AUTH_CONTEXT`, calls `GetHiddenAliases`, then `applyHiddenAliases`. The unknown-alias check runs after `requestIdleCallback`, falling back to `setTimeout(…, 0)`.
- [ ] **Step 5:** run `npm test && npm run build && bash scripts/check-xss.sh`. Expected: tests pass, `wwwroot/index.js` is rebuilt, and the grep finds nothing. Then run the CI type-check locally: copy `src` to a temp folder, `npm i --no-save @umbraco-cms/backoffice@^18.3.0`, `npx tsc --noEmit`. Expected: no errors.
- [ ] **Step 6:** write the release-checks Playwright smoke `tests/e2e/tidy.spec.ts` (`workers: 1`, `@umbraco/playwright-testhelpers`). It signs in as writer and as editor, using passwords from env vars, and asserts the Welcome tab and search header app per spec 3.3. Locators are scoped to their container and `.filter({ visible: true })`, never `.first()` on a page-wide locator (README rule 11). Run it locally against the 18 demo on HTTPS. Expected: `2 passed`. It never runs on PRs.
- [ ] **Checkpoint (Pascal commits):** `feat: backoffice entry point that excludes hidden aliases`

### Task 6: Docs, Marketplace and release workflow

**Files:** `README.md`, `CLAUDE.md`, `umbraco-marketplace.json`, `.github/workflows/publish.yml`

- [x] **Step 1:** README:
  - opens with the bold "Hiding is not access control" block (spec 5);
  - install, a supported-versions table (`17.x` on CMS 17.8+, `18.x` on 18.3+), the config table, presets, and how to find an alias (browser DevTools, `umbExtensionsRegistry.getAllExtensions()`);
  - per-environment and per-group examples, the Task 2 spike outcomes (flash or next-load), and uninstall.
- [x] **Step 2:** `CLAUDE.md` behaviours to preserve:
  - the server decides; the response is the caller's aliases only;
  - `exclude`, not `unregister`;
  - fetch failure means nothing hidden;
  - permanent names;
  - one bundle for both lines and why.
- [x] **Step 3:** `umbraco-marketplace.json`, with tags from spec 7 and Category "Editor Tools".
- [x] **Step 4:** `publish.yml`: on `release/*`, `scripts/pack.sh` (one nupkg per line, each with `wwwroot/index.js`), trusted publishing, `id-token: write` only there, no NuGet cache, check-vulnerable before pack. Run `actionlint`. Expected: no output. Then `unzip -l` on both nupkgs lists `staticwebassets/index.js`.
- [ ] **Step 5 (Pascal):** reserve the NuGet ID (`0.0.1-alpha`, push, unlist, 2FA on), and add a Trusted Publishing policy.
- [ ] **Checkpoint (Pascal commits):** `docs: README, Marketplace and release workflow`

### Task 7: First full GitHub CI run (Pascal, with the agent fixing)

README rule 14: done means green on GitHub, not just locally. Plan time for several fix rounds (01 needed five).

- [ ] **Step 1 (agent):** pre-push checks. `./scripts/test.sh` passes on both lines. `cd CLIENT && npm ci --ignore-scripts && npm test && npm run build`, then `git diff --exit-code -- SRC/wwwroot` shows no drift. `gitleaks dir . --redact` shows no leaks. `ls docs` shows only `design.md plan.md spikes.md`. `git log --all --name-only --format= | grep -iE "scope\.md|review|council"` prints nothing. `grep -rn "uses: " .github/workflows | grep -vE "@[0-9a-f]{40}"` prints nothing. `grep -rnE "password|Password" .github/workflows` shows only `openssl rand` and env-var names. The agent then states plainly in the handover: **CI has not run yet**.
- [ ] **Step 2 (Pascal):** push the feature branch and open a PR. Expected: `build` (four legs), `secrets` and `deps` and `client` are green in about 5 minutes.
- [ ] **Step 3 (Pascal):** run **Release checks** by hand on the branch (`gh workflow run release-checks.yml --ref <branch>`). Expected: both lines green (SQL Server boot and Playwright smoke).
- [ ] **Step 4 (agent):** fix each failure under the owning task's tests, one round at a time, until Steps 2 and 3 are green. Record anything that differed between local and GitHub in `docs/spikes.md`.
- [ ] **Checkpoint (Pascal commits):** `ci: fixes from the first GitHub run` (only if anything changed).

### Task 8: Final whole-branch review

- [ ] **Step 1:** dispatch `qa-reviewer` (fresh context, model `opus`) with the branch diff, `docs/*` and the Umbraco pitfalls reference. Ask for findings by severity against Global Constraints, Review Focus and spec 3.3, on both lines, with S1 and the "not access control" wording checked explicitly.
- [ ] **Step 2:** save the verdict to the private workspace (never in this repo). Fix blocking findings under the owning task's tests, then rerun Task 7 Steps 1-3. Expected: green locally and on GitHub, every job.
- [ ] **Checkpoint (Pascal commits):** `fix: final review findings`

### Task 9: Exercise it for real, on 17 and 18, then release

Run every step on **both** `demo/DemoSite.V17` and `demo/DemoSite.V18`, over HTTPS, with DevTools open (Console and Network, Fetch/XHR). Rules:

```json
"BackofficeTidy": { "Hide": [
  { "Preset": "WelcomeDashboard" },
  { "Preset": "Copilot", "Environments": ["Production"] },
  { "Alias": "Umb.HeaderApp.Search", "UserGroups": ["writer"] },
  { "Alias": "Umb.Dashbaord.Typo" }
] }
```

- [ ] **Step 1, as Wren Writer** (`ASPNETCORE_ENVIRONMENT=Development`):
  - no Welcome dashboard and no search header app;
  - one console warning naming `Umb.Dashbaord.Typo`;
  - `GET .../backoffice-tidy/hidden` returns exactly the three applicable aliases, with `no-store`.
- [ ] **Step 2, as Ed Editor:** no Welcome dashboard, the search header app is visible, and the response doesn't list `Umb.HeaderApp.Search`.
- [ ] **Step 3, as admin with `ASPNETCORE_ENVIRONMENT=Production`** (and Umbraco.AI installed where available): no Copilot button or sidebar. Switch to `Development` and restart: Copilot is back. Record any flash.
- [ ] **Step 4, negatives:**
  - stop the endpoint (temporarily misconfigure the route in a scratch build of the demo only, or block it in DevTools): nothing is hidden, and one warning shows;
  - anonymous `curl -sk` to the endpoint gives 401;
  - `{ "Alias": "x", "Preset": "Copilot" }` makes start-up fail naming `BackofficeTidy:Hide:0`.
- [ ] **Step 5:** confirm the README claim: as Wren, with the search header app hidden, `GET /umbraco/management/api/v1/item/document/search?query=a` still answers. Hiding isn't access control, as the README says.
- [ ] **Step 6:** no console errors from `BackofficeTidy.*` other than the deliberate warnings. No server log Errors.
- [ ] **Step 7 (Pascal):** run `release-checks.yml`, then push the tag `release/0.0-alpha.1`. Expected: `17.0.0-alpha.1` and `18.0.0-alpha.1` on NuGet. Run `gh release create --prerelease --generate-notes`, then post the forum and discussion replies (spec 7).
- [ ] **Checkpoint (Pascal commits):** `docs: acceptance results`

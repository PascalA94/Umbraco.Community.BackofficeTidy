# Spikes

Evidence for the decisions in `design.md`. Tags: **[verified]** observed in this repo, **[unverified]** still to check.

## Open at the start (design section 0)

- [unverified] Does the entry-point initializer call `unloadExtension` (the module's `onUnload`) when an already-loaded entry point is excluded? Needed for the Copilot preset.
- [unverified] How visible is a dashboard flash before exclusion?

## Scaffold notes (Task 1)

- [verified] Both lines build from one branch with `UmbracoMajor` (17 or 18, default 18). `./build/test.sh` loops over `eng/lines.txt`: 5 unit and 2 integration tests pass on each line.
- [verified] The two lines need separate `obj/` and `bin/` folders, and `DefaultItemExcludes` must exclude `obj/**;bin/**`. Without it the other line's generated files are globbed in (CS0579).
- [verified] The demo site serves either line: `-p:UmbracoMajor=17` or the `UmbracoMajor` environment variable. Use a separate database per line.
- [verified] MSBuild reads environment variables as properties, so CI picks the line and the floating "latest" versions through `UmbracoMajor`, `UmbracoTestVersion` and `UmbracoDemoVersion` env vars.

## Task 2: exclude timing (2026-11-05)

Method: a temporary `onInit` called `extensionRegistry.exclude(alias)` after a configurable delay (read from the page URL), on the demo sites for 17.8.0 and 18.3.0 (separate SQLite databases, headless Chromium, sampled every 100 ms). The temporary code was reverted afterwards; the rebuilt `wwwroot` is byte-identical to the pre-spike build.

### Welcome dashboard (`Umb.Dashboard.UmbracoNews`), both lines

- [verified] Excluded after 2 s: the Welcome tab was visible from about 0.5 s, **disappeared at the moment of exclusion without a reload**, and stayed gone.
- [verified] Excluded in the same tick as `onInit` (0 ms): the tab was **never seen** in 6 s of sampling. A tab that is present when the backoffice first renders flashes only for the delay.

### Copilot (`UmbracoAIAgent.BackofficeEntryPoint.CopilotSidebar`)

Installed Umbraco.AI 17.7.0 + Copilot 17.2.0 on the 17 site and Umbraco.AI 18.7.0 + Copilot 18.2.0 on the 18 site (both exist on NuGet; the 17 line does have a 17.x package). Copilot renders on both lines from about 0.5 s.

- [verified] Excluded at 30 ms or later, up to 2 s: the Copilot element is **removed without a reload**, on both lines. So `onUnload` does run when an already-loaded entry point is excluded. Flash is at most the delay.
- [verified] **Race:** excluded at 0 ms (both lines) and at 10 ms (17 only), Copilot **stays visible for good**. A second `exclude` of the same alias 150 ms later does not remove it. Excluding while Copilot's own entry point is still loading appears to lose.
- [verified] Delays of 100, 300, 600 and 1000 ms all removed Copilot on both lines.

### Decision

- The Copilot preset stays. `onUnload` runs on exclusion, so the README does not need "hides from the next page load".
- **Task 5 must not apply exclusions in the first few tens of milliseconds after `onInit` starts.** The real flow awaits an authenticated fetch, which is slower than that in practice, but a fast or cached response must not win the race. Apply exclusions no earlier than 100 ms after `onInit` begins (`await` the fetch, then wait out the remainder). Re-excluding later is not a fix. The cost is a flash of up to about 100 ms for a dashboard that was already rendered. Task 5's `Exclude_AfterRegister_Removes` uses a fake registry and cannot catch this race, so Task 9 step 3 must check Copilot on both lines.
- Same decision on both lines (the behaviour matched).
- [unverified] The mechanism of the 0 ms and 10 ms loss. Not needed for the decision.
- [verified] A direct re-navigation to `/umbraco/section/content?...` after sign-in logged two `Token request failed: 400` console errors on both lines, while the plain sign-in in the smoke spec logs none. It happened with the spike code only; recheck in Task 9 with the real entry point.

## Tasks 3 to 5: endpoint and entry point (2026-11-05)

- [verified] **OpenAPI differs per line, in `Web/OpenApi/BackofficeTidyOpenApi.cs`.** 18 uses `AddBackOfficeOpenApiDocument` (served at `/umbraco/openapi/backoffice-tidy.json`). 17.8 has no such helper, so `#if UMBRACO_17` registers a Swashbuckle document (served at `/umbraco/swagger/backoffice-tidy/swagger.json`) with a `BackOfficeSecurityRequirementsOperationFilterBase` and an `OperationIdHandler`. Both lines produce operation id `GetBackofficeTidyHidden` and the same response schema, so one generated client serves both.
- [verified] The operation id is `GetBackofficeTidyHidden`, not the plan's `GetHiddenAliases`: core derives it from the verb and route. The generated function is `getBackofficeTidyHidden`. Nothing outside the generated client uses the name.
- [verified] The endpoint's response carries a second property, `warnUnknownAliases` (a boolean). Design 3.2 shows only `aliases`, but the client has no other way to learn the `WarnUnknownAliases` setting. It reveals no rule, group or environment, and a test pins the two property names.
- [verified] **`exclude()` removes the alias from `isRegistered`.** Checking for unknown aliases after excluding would warn for every alias, so the client reads the unknown aliases first, excludes, and only then defers the warning to the idle tick. Consequence [unverified]: an alias registered by a package that loads after the exclusion is excluded without a warning, and a package that registers late can make a real alias look unknown. Recheck the typo warning in Task 9 on both lines.
- [verified] The spike's race result is honoured in code: `runHide` waits until 100 ms after `onInit` began before excluding (`MIN_EXCLUDE_DELAY_MS`, with unit tests for the fast and slow response). Copilot itself is still only checked in Task 9 step 3 (Umbraco.AI is not in the demo).
- [verified] Importing `@umbraco-cms/backoffice/extension-api` under Vitest hangs (it pulls in the whole backoffice). The tests import core's real `UmbExtensionRegistry` by its file path instead. The CI-style type-check against 18.3.1 and the 19 client tests pass with that import.
- [verified] `window.umbExtensionsRegistry` is `undefined` on 18.3 after sign-in, so the README must not tell people to type it in DevTools until Task 6 finds a route that works.
- [verified] Browser run, both lines (HTTPS, SQLite, Development): the writer's response lists the Welcome preset, the search header app and the typo alias, with `Cache-Control: no-store`; the editor's does not list the search header app and keeps the search button; each logs exactly one `BackofficeTidy` warning (the typo). `Alias` plus `Preset` in one rule stops start-up with `BackofficeTidy:Hide:0 sets both Alias and Preset`.
- [verified] Umbraco rewrites the demo's `appsettings.json` on first start (a `Global:Id` and an `Imaging:HMACSecretKey`, which gitleaks flags). Run the demo with `Umbraco__CMS__Global__Id` and `Umbraco__CMS__Imaging__HMACSecretKey` set in the environment so nothing is written, and check the diff of that file before staging.

## Task 6: docs and packing (2026-11-05)

- [verified] **Finding an alias without `umbExtensionsRegistry`:** the backoffice ships an Extension Insights workspace, menu item `Umb.MenuItem.Extensions` in `Umb.Menu.AdvancedSettings` (Settings, Advanced Settings), present in `@umbraco-cms/backoffice` 17.8.1 and 18.3.1 (read from the packages' manifests). The README points there. [unverified] that it renders for each user group and lists third-party aliases; check it by eye in Task 9.
- [verified] `build/pack.sh` produces `17.0.0-alpha.1` and `18.0.0-alpha.1` nupkgs and snupkgs. Each contains `staticwebassets/index.js`, its chunk and `umbraco-package.json`, with the line's version stamped into the manifest; the manifest in the working tree is restored afterwards. The nuspec dependency ranges are `[17.8.0, 18.0.0)` and `[18.3.0, 19.0.0)`.
- [verified] The options are read through `IOptionsMonitor`, so a reloaded appsettings file applies on the next backoffice load without a restart. [unverified] by hand.
- [unverified] `publish.yml` (tag `release/*`) has only been linted with `actionlint`; its build, pack and Trusted Publishing steps have not run on GitHub.

## Tasks 8 and 9: final review fixes and acceptance (2026-11-05)

- [verified] **Copilot survives an exclude that lands while its module is still loading, over a slow link.** With Umbraco.AI 18.7.0 and Copilot 18.2.0, Slow 4G (150 ms, 1.6 Mbit/s) and the cache off, the Copilot button stayed for the whole 25 s sample on 18, even with the 100 ms floor. On 17 it left after about 200 ms. Cause (read from `UmbExtensionInitializerBase`): core unloads an entry point only if its module has finished loading, so an exclude during the load is lost. Fix: `runHide` excludes a `backofficeEntryPoint` alias only after its own module's import has resolved plus 50 ms (10 s timeout, and a failed import still excludes). After the fix the same run leaves Copilot gone on both lines (longest time seen: 0 ms on 18, about 200 ms on 17 before the change). The 100 ms floor still applies to everything else.
- [verified] The Copilot button and sidebar are `uai-copilot-fab` and `uai-copilot-sidebar`. On 18 the fab is in the page but not "visible" to Playwright, so the check counts the elements. In Development both are present; in Production both are absent, on both lines, at normal speed.
- [verified] Anonymous `GET .../backoffice-tidy/hidden` gives 401; with the request aborted or answered 500, nothing is hidden and exactly one warning shows; `{Alias: x, Preset: WelcomeDashboard}` at `BackofficeTidy:Hide:0` stops start-up (exit 134, `BackofficeTidy:Hide:0 sets both Alias and Preset; set exactly one.`). All on both lines.
- [verified] The Management API does not accept the backoffice cookie; the backoffice sends a bearer token. As the writer, with the search header app hidden, `GET /umbraco/management/api/v1/item/document/search?query=a` with that token answers 200 on both lines (hiding is not access control). Server logs showed no `[ERR]` or `[FTL]` lines in any run.
- [verified] Welcome tab control: with the rules request blocked the tab "Welcome to Umbraco" and the search header app are found by the same locators the absence checks use. When Welcome is hidden the writer has one dashboard left, so there is no tab bar at all.
- [verified] Running the demo with `Umbraco__CMS__Global__Id` and `Umbraco__CMS__Imaging__HMACSecretKey` set leaves the tracked `appsettings.json` untouched. Without them Umbraco writes both, and the key trips gitleaks.
- [unverified] On GitHub: nothing has run. The array-merge behaviour of two config files (README) is stated from framework behaviour and was not run here.

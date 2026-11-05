# CLAUDE.md

`Umbraco.Community.BackofficeTidy`: Hide Umbraco backoffice extensions by alias, per environment and user group, from appsettings.
Razor class library in `src/Umbraco.Community.BackofficeTidy/` (`Core/` is line-agnostic, `Web/` is per Umbraco line).
Design `docs/design.md`, plan `docs/plan.md`, spikes `docs/spikes.md`, where work stopped: `COMMITS-TODO.md`, `HANDOVER.md`.

**Never commit, push or publish.** Pascal commits, pushes, tags and releases.

## Lines and versions

- **One branch, two lines.** `UmbracoMajor` (`17` or `18`, default `18`, set as a property or an environment variable) picks the CMS range (`[17.8.0, 18.0.0)` or `[18.3.0, 19.0.0)`), the package version (`17.x.y` or `18.x.y`), the `UMBRACO_17` / `UMBRACO_18` constants and its own `packages.<line>.lock.json`, `obj/<line>` and `bin/<line>`. `eng/lines.txt` lists the lines; `build/test.sh` runs the suites once per line.
- **One client bundle serves both lines.** `@umbraco-cms/backoffice` is a Vite external, the committed `wwwroot/` is built against `^17.8.0`, and CI type-checks the source against `^18.3.0`. Split into two bundles only if a line ever needs different code.
- The package version is `<UmbracoMajor>.<PackageMinor>.<PackagePatch>-<suffix>` from `Directory.Build.props`. Client version files stay `0.0.0`; the pack step stamps the line's version into the nupkg's manifest.
- The NuGet ID, App_Plugins folder, aliases (`BackofficeTidy.*`), route `backoffice-tidy`, action `hidden`, OpenAPI group `Backoffice Tidy` and the preset names are permanent once released.

## Commands

```bash
build/test.sh            # restore, build and test every line in eng/lines.txt (SQLite); or build/test.sh 17
dotnet restore src/Umbraco.Community.BackofficeTidy.slnx -p:UmbracoMajor=17 --locked-mode   # one line, locked
# SQL Server integration run: set Tests__Database__DatabaseType=SqlServer and Tests__Database__SQLServerMasterConnectionString
# Client (output is committed in wwwroot/; CI fails on drift)
cd src/Umbraco.Community.BackofficeTidy/Client && npm ci --ignore-scripts && npm run build && npm test
bash build/check-xss.sh   # from the repo root
# Demo site: HTTPS only (OpenIddict refuses HTTP)
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.BackofficeTidy.DemoSite
# Demo users Wren Writer and Ed Editor: set Demo:WriterPassword / Demo:EditorPassword (user-secrets or Demo__* env vars)
dotnet dev-certs https && dotnet run --project demo/Umbraco.Community.BackofficeTidy.DemoSite -p:UmbracoMajor=17   # https://localhost:44331/umbraco; use a separate database per line
# e2e (demo running; env vars as in tests/e2e/.env.example)
cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test --reporter=line
bash build/check-vulnerable.sh
```

## Behaviours to preserve

- **The server decides.** `GET backoffice-tidy/hidden` returns only the signed-in caller's aliases (plus the `warnUnknownAliases` flag), with `Cache-Control: no-store`. It is authenticated (`ManagementApiControllerBase`, never `[AllowAnonymous]`) and never returns the rule list, other groups' rules or environment names.
- **`exclude`, not `unregister`.** The client calls `extensionRegistry.exclude(alias)`, no sooner than 100 ms after `onInit` starts (an earlier exclude loses a race with Copilot's own entry point, `docs/spikes.md`).
- **A failed fetch hides nothing** and logs one warning. A typo'd alias warns once and changes nothing else.
- **Hiding is not access control.** The README says so first; keep it first.
- **Permanent names** (above). Preset aliases live in one C# table (`Core/Configuration/Presets.cs`) with a pin test.
- **One client bundle for both lines**, because `@umbraco-cms/backoffice` is an external and the two lines' APIs used here match. Split only if a line needs different code.
- **No `innerHTML`, `unsafeHTML` or `unsafeStatic`** in the client (`build/check-xss.sh`).

## Releasing

- Bump `PackageMinor`, `PackagePatch` and `PackageVersionSuffix` in `Directory.Build.props` (both lines move together), and rebuild the client if its source changed.
- `bash build/check-vulnerable.sh` and `bash build/check-release-version.sh release/<minor>.<patch>[-suffix]` both exit 0.
- Run the **Release checks** workflow by hand on the release branch (SQL Server integration, and e2e if present). Never on PRs. Re-run after a new Umbraco minor.
- Squash-merge, tag `release/<minor>.<patch>[-suffix]` on the merge commit; `publish.yml` (tag `release/*`, from `main` only) restores, builds, tests and audits both lines, packs them with `build/pack.sh` (one nupkg per line, each checked for `staticwebassets/index.js`) and publishes with Trusted Publishing. It has not run on GitHub yet. Then `gh release create` per line, and mark the newest Umbraco line's release as Latest.
- Stable from the first real public release unless the API is still moving (`.github/REPO-SETUP.md` step 10); after that, no breaking changes within a major.
- "Done" means green on GitHub. The first CI run is its own step with Pascal.

## Keep

- Running the demo makes Umbraco rewrite the tracked `demo/.../appsettings.json` (a site id, reformatted arrays, and an imaging `HMACSecretKey` unless one is supplied). Set `Umbraco__CMS__Imaging__HMACSecretKey` and `Umbraco__CMS__Global__Id` as env vars when running it, and `git checkout` that file before `git add`.
- Floors never below 17.8.0 / 18.3.0. The repo is public: no reviews, plans of record or local paths in it.
- `docs/` holds only `design.md`, `plan.md`, `spikes.md`. Fake test secrets carry `// gitleaks:allow` from their first commit.

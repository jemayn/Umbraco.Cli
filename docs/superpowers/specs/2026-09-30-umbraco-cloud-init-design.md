# `umbraco cloud init` — design

Date: 30-09-2026
Status: approved design, implemented (see [the plan](../plans/2026-09-30-umbraco-cloud-init.md))

## Summary

`umbraco cloud init` prepares an Umbraco project for Umbraco Cloud. For an existing Umbraco project it adds the required Umbraco Cloud packages (and chosen Products), the Cloud licence settings, `umbraco-cloud.json` and, for Git deployments, `.umbraco`. When asked to create a new Umbraco project, it first creates one with the Umbraco.Templates `umbraco` template, laid out like the Cloud scaffold, and then does the same.

Terminology follows [GLOSSARY.md](../../../GLOSSARY.md) (notably Umbraco project, Cloud project, Cloud scaffold, Product, Integration package, Release type, Lifecycle phase). Decisions recorded as ADRs: [0003](../../adr/0003-never-embed-the-identity-secret.md) (Identity values), [0004](../../adr/0004-calculate-release-types-and-lifecycle-phases.md) (release types and lifecycle phases), [0005](../../adr/0005-query-nuget-with-httpclient-and-nuget-versioning.md) (NuGet access). It extends the [0.1.0 design](2026-09-30-umbraco-cli-design.md) and reuses its conventions (output, exit codes, `JsonDocumentEditor`, `IDotnetCli`, project location).

## Sources

- NuGet v3 registration data for Umbraco.Templates, Umbraco.Cms, Umbraco.Cloud.Cms, Umbraco.Cloud.StorageProviders.AzureBlob(.ImageSharp), Umbraco.Deploy.Cloud, Umbraco.Forms, Umbraco.Forms.Deploy and the other Umbraco Products (fetched 30-09-2026).
- The Cloud scaffold that Umbraco Cloud uses for new Cloud projects (17 and 18), confirmed with the Umbraco Cloud team.
- The Identity configuration read by the Umbraco.Cloud.Cms package, and how Umbraco Cloud provisions the `Identity` block in `umbraco-cloud.json`.
- Umbraco Cloud storage providers (github.com/umbraco/Umbraco.Cloud.StorageProviders.AzureBlob): composers register Azure Blob storage only when Cloud provides its settings.
- Umbraco LTS and end-of-life policy: https://umbraco.com/products/knowledge-center/long-term-support-and-end-of-life/
- `dotnet new umbraco --help` for Umbraco.Templates 13.16.2, 17.x and 18.2.0; `dotnet package add --help` (SDK 10.0.401).

## Constraints and facts the design relies on

- Every Umbraco Product package uses the Umbraco CMS major as its own major (Cloud.Cms, Cloud.StorageProviders.AzureBlob, Deploy.Cloud, Forms, Forms.Deploy, Commerce, Engage, …), and Umbraco Cloud requires them to match the CMS major. Some versions need a newer minor than `x.0` (e.g. Umbraco.Automate 17.4.0 needs CMS ≥ 17.6.0); CMS constraints are often only transitive and ranges are inconsistent (`[17.0.0, 18.0.0)`, `[15.0.0, 16.0.0]`, `[16.0.0, )`).
- `Umbraco.Cloud.StorageProviders.AzureBlob` depends on `Umbraco.Cloud.StorageProviders.AzureBlob.ImageSharp`, so the latter is not added explicitly (as in the Cloud scaffold).
- No code changes are needed: Umbraco.Cloud.Cms and the storage providers register themselves through composers, and storage only switches to Azure Blob when Umbraco Cloud provides its settings.
- The `umbraco` template has no option to choose the CMS version (only Templates 13 had one): a version is chosen by installing that version of Umbraco.Templates. Installing it into the user's template store replaces their installed version; `--debug:custom-hive <dir>` isolates it. Template options differ per major (e.g. `--package-management` exists from 17).
- The Cloud scaffold (17 and 18 identical): `dotnet new umbraco -n UmbracoProject --use-https-redirect --no-nodes-view-path "~/umbraco/UmbracoWebsite/Deploy/NoNodes.cshtml" --package-management Central`, the project in `src/UmbracoProject/`, `Directory.Packages.props` moved to the repository root, and at the root `.umbraco`, `.gitignore`, `.dockerignore`, `NuGet.config` and `README.md` (its `.editorconfig` and `.gitattributes` are empty); the project gets `appsettings.Staging.json`, `appsettings.Production.json` and, in `appsettings.json`, `Umbraco:Licenses:Products` with `Umbraco.Deploy` and `Umbraco.Forms` set to `UMBRACO-CLOUD` (no licence files from 17). `umbraco-cloud.json` is shipped as `{}` and filled in by Umbraco Cloud during provisioning.
- `.umbraco` (TOML at the repository root) is required by Umbraco Cloud's build, upgrades and package updates: `[project]` with `base = "src/UmbracoProject"` and `csproj = "UmbracoProject.csproj"`.
- Umbraco.Cloud.Cms always registers Umbraco ID login and throws "No ClientId set." when `Identity:ClientId` (or `ClientSecret`) is empty; there is no fallback. With non-empty values the regular username/password backoffice login keeps working. Umbraco Cloud writes the same `Identity` values (per Cloud stamp) into every Cloud project's `umbraco-cloud.json`; only `EnvironmentId` differs per Environment. No external API returns them (see [Future](#future-umbraco-cloud-login)).
- `dotnet package add <id> --version <v> --project <csproj>` supports central package management (writes `PackageVersion` to `Directory.Packages.props`), but silently updates (or downgrades) an existing version, so existing references must be checked first.
- Only `rc` and `beta` prerelease labels are used by Umbraco.Templates/Umbraco.Cms on NuGet.

## Command

```
umbraco cloud init [--project <csproj|dir>] [--deployment-type artifact|git] [--name <name>] [--output <dir>] [--version <version>] [--development-mode ide|backoffice] [--exclude-gitignore]
                   [--add <product>]... [--identity-client-id <id>] [--identity-client-secret <secret>]
                   [--allow-end-of-life] [--no-build] [--non-interactive] [-o text|json] [-- <template options>]
```

| Option | Applies to | Meaning |
|---|---|---|
| `--project` | existing | The Umbraco project (`.csproj` or a directory containing one); default: the single `.csproj` in the current directory |
| `--deployment-type` | both | `artifact` (default): the Umbraco project is deployed as published output (an Artifact deployment); `git`: as source code (a Git deployment), which also writes `.umbraco` and the root `README.md` (the Cloud scaffold's description of the git-based workflow) |
| `--name` | new | Create a new Umbraco project with this name (default when prompted: `UmbracoProject`) |
| `--output` | new | The repository root to create it in (default: the current directory; it may contain other files, but none of the ones created) |
| `--version` | new | `latest` (default), `lts`, `rc`, `beta`, `next`/`preview`, a major (`17`), major.minor (`17.2`) or exact version (`17.2.0`) |
| `--development-mode` | new | `ide` (default): the template's `IDEDevelopment` mode, i.e. Production runtime mode and `ModelsMode: Nothing` in `appsettings.json`, Development runtime mode and `SourceCodeAuto` models locally; `backoffice`: `BackofficeDevelopment`, as in the Cloud scaffold. A `-dm`/`--development-mode` template option wins |
| `--exclude-gitignore` | new | Create no `.gitignore` files (passed to the template as `--exclude-gitignore`) |
| `--add` | both | A Product key from the catalogue (repeatable); `forms` in the first version |
| `--identity-client-id`, `--identity-client-secret` | both | Umbraco ID values for `umbraco-cloud.json`; also read from `UMBRACO_CLOUD_IDENTITY_CLIENT_ID` and `UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET` |
| `--allow-end-of-life` | new | Allow creating a project on an end-of-life major |
| `--no-build` | both | Restore, but don't build |
| `--non-interactive` | both | Never prompt; use defaults or fail |
| `-- <…>` | new | Passed unchanged to `dotnet new umbraco` (e.g. `-- --development-database-type SQLite`) |

`--version`, `--output`, `--development-mode`, `--exclude-gitignore`, `--allow-end-of-life` and template options with `--project` (or an existing project without `--name`) exit 2.

### Mode

1. If `--name` is given, create a new Umbraco project (below), then continue with it as an existing Umbraco project.
2. Otherwise locate the Umbraco project like `umbraco deploy init` (`--project`, else the single `.csproj` in the current directory; several ⇒ exit 2 listing them).
3. If there is no `.csproj` and the terminal is interactive, prompt for a name and create a new Umbraco project; non-interactive ⇒ exit 2 with a hint to use `--name` or `--project`.

Only Umbraco CMS 17 and later is supported; an older resolved CMS version ⇒ exit 2. `umbraco cloud init` never runs git commands.

## Creating a new Umbraco project

1. **Validate the output**: `--output` (default: current directory) must not already contain any of the paths the Umbraco project creates (`src/<name>`, `.umbraco` for Git deployments, `<name>.slnx`, `Directory.Packages.props`, the root `.gitignore` unless excluded, and the root scaffold files), otherwise exit 2 listing them; other files (e.g. a README, LICENSE or a local package feed) are fine. `--name` must be a valid project name (letters, digits, `.`, `_`, `-`; not starting with a digit).
2. **Resolve the version** (see [Versions](#versions)); prompt when interactive and `--version` isn't given.
3. **Check the lifecycle phase** of the major: security phase ⇒ warning; end-of-life ⇒ exit 2 unless `--allow-end-of-life`.
4. **Install the template** into an isolated, cached template store: `dotnet new install Umbraco.Templates@<version> --debug:custom-hive <cache>/templates/<version>` (skipped when that store already has it). `<cache>` is `%LOCALAPPDATA%\Umbraco.Cli` on Windows and `$XDG_CACHE_HOME/umbraco-cli` (default `~/.cache/umbraco-cli`) elsewhere.
5. **Create the project**: `dotnet new umbraco -n <name> -o <output>/src/<name> --use-https-redirect --no-nodes-view-path "~/umbraco/UmbracoWebsite/Deploy/NoNodes.cshtml" --package-management Central --development-mode IDEDevelopment|BackofficeDevelopment --minimal-gitignore|--exclude-gitignore --no-restore --no-update-check --debug:custom-hive <store> [template options]`. The Cloud scaffold itself uses the template's default (`BackofficeDevelopment`); `IDEDevelopment` is the CLI's default because Production runtime mode suits published (artifact) deployments. Umbraco Deploy doesn't depend on the runtime mode.
6. **Apply the Cloud scaffold**: move `src/<name>/Directory.Packages.props` to the root; unless `--exclude-gitignore`, create the .NET `.gitignore` at the root with `dotnet new gitignore` and add the Umbraco Deploy trigger and status files (`/umbraco/Deploy/deploy*`) to the template's minimal (Umbraco-specific) `src/<name>/.gitignore`; write the embedded scaffold files (see [Cloud scaffold resources](#cloud-scaffold-resources)) with `<name>` substituted; for Git deployments, write `.umbraco` with `base = "src/<name>"` and `csproj = "<name>.csproj"` (Umbraco Cloud builds a Git deployment from the project it points at; an Artifact deployment is already built).
7. **Create the solution**: `dotnet new sln -n <name> -o <output> --format slnx` and `dotnet sln <name>.slnx add src/<name>/<name>.csproj`; then add a `/Solution Items/` folder listing the root files (`.gitignore` unless excluded, `Directory.Packages.props`, and `.umbraco` and `README.md` for Git deployments) directly in the `.slnx`, because `dotnet sln` can't add files.

## Preparing an Umbraco project

1. **Detect the CMS version** with `dotnet package list --project <csproj> --include-transitive --format json` (the existing `IDotnetCli`): the resolved version of `Umbraco.Cms.Core`. Missing ⇒ exit 2 ("not an Umbraco project"); major < 17 ⇒ exit 2; lifecycle security/end-of-life ⇒ warning.
2. **Plan the packages**: the required packages, the packages of the chosen Products (`--add`, or a multi-select prompt with nothing selected when interactive) and the Integration packages whose Products are all present (see [Product catalogue](#product-catalogue)). A package that is already referenced (top-level) is kept as-is and reported as skipped, with a warning when its major differs from the CMS major. A Product that has no stable version for the CMS major ⇒ exit 2 for `--add`; an unavailable Integration package ⇒ warning.
3. **Resolve versions** for the packages to add (see [Package versions](#package-versions)), then add each with `dotnet package add <id> --version <v> --project <csproj> --no-restore`.
4. **Licences**: set `Umbraco:Licenses:Products:<licence key>` to `UMBRACO-CLOUD` in the project's `appsettings.json` for each Product that is present afterwards (added or already referenced) and has a licence key (`Umbraco.Deploy`, and `Umbraco.Forms` when Forms is present), through `JsonDocumentEditor`; existing values are kept.
5. **`umbraco-cloud.json`** (next to the project file): if missing, create it with an `Identity` block (see [Identity values](#identity-values)); if present, leave it, unless explicit Identity values were given, which then set `Identity:ClientId` and `Identity:ClientSecret`.
6. **`.umbraco`** (Git deployments only): if missing, write it at the git repository root (the nearest parent containing `.git`), else the nearest parent containing a `.sln` or `.slnx`, else the project directory, with `base` and `csproj` relative to that root; an existing `.umbraco` is kept, with a warning when it points at a different project.
7. **Restore and build**: `dotnet restore <csproj>`, then `dotnet build <csproj> --no-restore` unless `--no-build`; a failure ⇒ exit 1 with the tool's output.
8. **Hints**: the `.gitignore` entries the Cloud scaffold uses that are missing from the repository's `.gitignore` (e.g. `**/wwwroot/media`, `**/umbraco/Logs/`, `**/umbraco/Data/TEMP/`, `**/umbraco/Deploy/deploy*`); `.gitignore` itself is never changed. `Program.cs` is never changed.

## Versions

`--version` (new Umbraco projects) resolves against the Umbraco.Templates versions on NuGet:

| Value | Resolves to |
|---|---|
| `latest` (default) | The newest stable version |
| `lts` | The newest stable version of the newest LTS major |
| `rc`, `beta` | The newest prerelease version with that label, of any major |
| `next`, `preview` | The newest prerelease version of any label |
| `17`, `17.2` | The newest stable version within that major or minor |
| `17.2.0`, `18.2.0-rc` | That exact version (must exist) |

Resolved versions below 17 ⇒ exit 2. The interactive prompt lists each supported major that isn't end-of-life with its newest stable version, release type and lifecycle phase (security-phase majors marked), preselecting `latest`; `--non-interactive` uses `latest`.

### Release types and lifecycle phases

- **Release type**: a major ≥ 17 is LTS when `(major − 17) % 4 == 0` (17, 21, 25, …), otherwise STS.
- **Lifecycle phase**, measured from the NuGet `published` date of the major's first stable `Umbraco.Cms` version (`x.0.0`):

| Release type | Active | Support | Security | End-of-life |
|---|---|---|---|---|
| STS | 0–6 months | 6–9 months | 9–12 months | after 12 months |
| LTS | 0–6 months | 6–24 months | 24–36 months | after 36 months |

A major without a stable release is in *prerelease*. The current date comes from `TimeProvider`.

## Package versions

For each package to add, with the Umbraco project's resolved CMS version *c*:

1. Take its listed versions in major *c*.major (stable only, unless *c* is a prerelease, then also prereleases of that major), newest first.
2. Pick the first version whose `Umbraco.*` dependency graph accepts *c*: for each dependency on an `Umbraco.Cms*` package the range must satisfy *c*; for each dependency on another `Umbraco.*` package, the newest listed version satisfying its range is checked the same way (recursively, memoised per package and version). Non-Umbraco dependencies are ignored.
3. No acceptable version ⇒ exit 2 for a required or `--add` package, warning for an Integration package.

### NuGet access

- nuget.org only, through `HttpClient` and source-generated System.Text.Json, behind `INuGetClient` (typed client in DI, faked in tests with `FakeHttpMessageHandler`).
- The service index (`https://api.nuget.org/v3/index.json`) is read once per run to discover `RegistrationsBaseUrl/3.6.0` (SemVer 2, gzip); registration pages are followed when not inlined. Responses are cached in memory for the run.
- Per version the client uses `version`, `listed`, `published` and `dependencyGroups` (for `net10.0`, falling back to the nearest compatible group).
- Version ordering, prerelease labels and dependency ranges use `NuGet.Versioning` (no dependencies, trim-safe). `VersionRange.FindBestMatch` returns the *lowest* match, so "newest compatible" is computed by the CLI.
- The project's own `nuget.config` feeds are not queried; installs go through `dotnet package add`/`dotnet restore`, which honour them.

## Product catalogue

An embedded JSON resource (`Cloud/Init/catalog.json`):

```json
{
  "requiredPackages": ["Umbraco.Cloud.Cms", "Umbraco.Cloud.StorageProviders.AzureBlob"],
  "products": [
    { "key": "deploy", "name": "Umbraco Deploy", "packages": ["Umbraco.Deploy.Cloud"], "licenseKey": "Umbraco.Deploy", "required": true },
    { "key": "forms", "name": "Umbraco Forms", "packages": ["Umbraco.Forms"], "licenseKey": "Umbraco.Forms" }
  ],
  "integrationPackages": [
    { "package": "Umbraco.Forms.Deploy", "products": ["forms", "deploy"] }
  ],
  "scaffold": { "verifiedMajors": [17, 18] }
}
```

- `requiredPackages` and required Products (Umbraco Deploy) are always installed; `--add` accepts the keys of the other Products.
- Availability per major is taken from NuGet (a Product without a stable version in the CMS major is unavailable); no per-major data is kept in the catalogue.
- A remote catalogue that overrides the embedded one is future work; the format stays the same.

## Cloud scaffold resources

- Embedded resources under `Cloud/Init/Scaffold/`, named with a `.template` suffix (so they don't affect this repository): `README.md` (Git deployments only) for the repository root, and `appsettings.Staging.json` and `appsettings.Production.json` for the project, copied from the Cloud scaffold of 17/18. `.umbraco` is generated. The Cloud scaffold's `.gitignore` is replaced by the .NET and template ones above, and its `.dockerignore` isn't used (the template adds Docker files itself with `-- --add-docker`). The Cloud scaffold's nuget.org-only `NuGet.config` isn't used either, so the project restores from the configured package sources (including any local or private feeds). In the README, `UmbracoProject` is replaced by the project name, and its file tree lists only the root files created here.
- Optional per-major overrides under `Cloud/Init/Scaffold/{major}/` replace a file for that major.
- Creating a project on a major that isn't in `scaffold.verifiedMajors` prints a warning that the Cloud scaffold wasn't verified for it.
- Licence settings are applied through the same code path as for existing projects.

## Identity values

`umbraco-cloud.json` (created when missing) contains only:

```json
{
  "Identity": {
    "ClientId": "<value>",
    "ClientSecret": "<value>"
  }
}
```

- The values are `--identity-client-id`/`--identity-client-secret`, else `UMBRACO_CLOUD_IDENTITY_CLIENT_ID`/`UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET`, else placeholders: `00000000-0000-0000-0000-000000000000` and `local-development-placeholder` (ADR 0003).
- The values in `umbraco-cloud.json` are only used for local Umbraco ID login (the same for all Cloud projects); each Umbraco Cloud environment gets its own isolated ClientId and ClientSecret through environment variables, so deploying the placeholders (e.g. with published output) doesn't affect Umbraco ID login on Umbraco Cloud.
- With placeholders the project boots and the regular backoffice login works locally; local Umbraco ID login needs the real local-login values (e.g. from a cloned Cloud project's `umbraco-cloud.json`). The text output says so and names the environment variables.
- The ClientSecret is never logged or written to output (JSON output reports `identity` as `"placeholder"`, `"provided"` or `"unchanged"` when an existing `umbraco-cloud.json` was left alone).

## Output

Text: the created/updated paths, each package added (with version and reason: required, `--add <key>` or Integration package) or skipped (already referenced, with version), warnings, the restore/build result and next steps: `umbraco deploy init` (the ApiSecret and environments), running the project locally first (so Umbraco installs and Umbraco Deploy writes the schema to `umbraco/Deploy/Revision` before publishing), and deploying it as published output (`dotnet publish` and `umbraco cloud deployments deploy --path publish`) or as source code (`--path <root>`, which needs a git work tree).

JSON (public contract):

```json
{
  "created": true,
  "deploymentType": "artifact",
  "repositoryRoot": "…",
  "projectPath": "…/src/MySite/MySite.csproj",
  "cmsVersion": "17.7.0",
  "releaseType": "LTS",
  "lifecyclePhase": "support",
  "packages": [ { "id": "Umbraco.Deploy.Cloud", "version": "17.3.1", "action": "added", "reason": "required" } ],
  "filesWritten": [ "…/src/MySite/appsettings.Production.json", "…/src/MySite/umbraco-cloud.json" ],
  "identity": "placeholder",
  "built": true
}
```

## Errors and exit codes

As in the 0.1.0 design. Additionally: unsupported CMS major, non-empty `--output`, invalid `--version`/`--add`, end-of-life without `--allow-end-of-life`, and new-project options with an existing project ⇒ 2; failures of `dotnet new`/`sln`/`package add`/`restore`/`build` or NuGet ⇒ 1 with the tool's output (NuGet requests are retried on 429/5xx/network errors like the Cloud API polls).

## Testing

- **Unit**: version alias resolution (fake NuGet data), release types and lifecycle phases (fake `TimeProvider` around phase boundaries), package version resolution (recursive `Umbraco.*` ranges, prerelease CMS, no match), catalogue planning (required, `--add`, Integration packages, already referenced, major mismatch), `.umbraco` placement, scaffold file generation (placeholders, overrides), `umbraco-cloud.json` and licence edits (inline expected documents), the `.slnx` Solution Items edit.
- **NuGet client**: service index discovery, paged registrations, gzip, caching, retries (fake `HttpMessageHandler`).
- **Commands**: end-to-end through `CliApplication` with fake `IDotnetCli` (recording `dotnet new`/`sln`/`package add`/`restore`/`build` invocations) and fake NuGet, for new and existing projects, text and JSON.
- **Integration** (`[Category("Integration")]`): real `dotnet new install` into a temporary custom hive and `umbraco cloud init --name` for the newest stable version, followed by `dotnet build`; needs network access.

## Out of scope

- Products other than Forms (Automate, AI, Workflow, Commerce, UI Builder, Engage and their Integration packages) until their Umbraco Cloud licensing is settled; the catalogue format supports them.
- A remote catalogue; `--dry-run`; git operations; migrating an existing repository to the Cloud scaffold layout; changing `Program.cs` or `.gitignore`; Umbraco CMS majors before 17.

## Future: `umbraco cloud login`

No external API returns a Cloud project's stamp, Environments or `Identity` values today. Retrieving them for `--project-id` would need a user login and a new endpoint:

- A new Umbraco ID (Azure AD B2C) public client using authorization code with PKCE and a loopback redirect (B2C doesn't support the device code flow).
- A user-authorised endpoint on the public Umbraco Cloud API (e.g. `GET /projects/{projectId}/local-development`) returning the Environments and the `Identity` block, with access checked server-side.
- Tokens stored in the operating system's credential store, never in plain files.
- A decision by the Umbraco Cloud and Umbraco ID teams on handing out the stamp-wide Identity values to authorised users.

## Open items

- Confirm with the Umbraco Cloud team that the Cloud scaffold files embedded in the CLI may be redistributed, and who notifies the CLI maintainers when the scaffold changes for a new major.

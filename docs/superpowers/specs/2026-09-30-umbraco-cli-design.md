# Umbraco CLI — design

Date: 30-09-2026
Status: approved design, implemented as a 0.1.0 proof of concept (see [the plan](../plans/2026-09-30-umbraco-cli-0.1.0.md))

## Summary

`Umbraco.Cli` is a .NET global tool (command `umbraco`) modelled on the Azure CLI's `group subgroup verb` structure. Release 0.1.0 contains two command groups:

- `umbraco cloud deployments upload|deploy|show` — upload Artifacts to and start Deployments on Umbraco Cloud via the CI/CD API v2.
- `umbraco deploy init` — ensure an ApiSecret is configured and Environments are added for Umbraco Deploy, writing `appsettings.json` (Umbraco Deploy On-Premises) or `umbraco-cloud.json` (Umbraco Deploy Cloud).

Terminology follows [GLOSSARY.md](../../../GLOSSARY.md). Two decisions are recorded as ADRs: [0001](../../adr/0001-apisecret-in-appsettings-json.md) (ApiSecret in `appsettings.json`) and [0002](../../adr/0002-cloud-apisecret-only-without-apikey.md) (Cloud ApiSecret only without ApiKey).

## Sources

- Umbraco Cloud CI/CD API v2 docs: https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd/umbraco-cloud-api
- Umbraco Deploy install/configure docs: https://docs.umbraco.com/umbraco-deploy/13.latest/installation/install-configure
- Umbraco Cloud CI/CD API behaviour beyond the public documentation (deployment states, validation, limits), confirmed with the Umbraco Cloud team
- Umbraco Cloud's deployment type detection and Git/Artifact deployment behaviour, confirmed with the Umbraco Cloud team
- Umbraco Deploy configuration binding: `umbraco/Umbraco-Deploy` — `src/Umbraco.Deploy.Cloud/Extensions/UmbracoBuilderExtensions.cs`, `src/Umbraco.Deploy.Cloud/DeployCloudConstants.cs`, `src/Umbraco.Deploy.Core/Configuration/DeployConfiguration/DeploySettings.Authentication.cs`, `src/Umbraco.Deploy.Infrastructure/Configuration/ValidateDeploySettings.cs`
- Release conventions: `umbraco/Umbraco-Deploy` — `version.json`, `.github/workflows/*.yml`, `azure-pipelines.yml`, `generate-sbom.yml`
- Test stack: `umbraco/Umbraco-CMS` — `tests/Directory.Packages.props`

## Constraints and facts the design relies on

- **CI/CD API v2**: base `https://api.cloud.umbraco.com`, header `Umbraco-Cloud-Api-Key`, key scoped to one Project. Rate limit: 10 calls per 60 s per key.
- **Upload** `POST /v2/projects/{projectId}/deployments/artifacts`, multipart fields `file` (required, `.zip` extension), `description`, `version`; max 256 MiB. Cloudflare in front of `api.cloud.umbraco.com` allows request bodies up to 500 MB, so the API limit is the effective one. Response: `artifactId, fileName, blobUrl, fileSize, createdUtc, description, version, detectedDeploymentType` (always null on v2).
- **Start** `POST /v2/projects/{projectId}/deployments`, body `artifactId`, `targetEnvironmentAlias`, `commitMessage` (all three required non-empty), `noBuildAndRestore`, `skipVersionCheck`, `skipPreserveUmbracoCloudJson` (default false), `runSchemaExtraction` (default true). 201 on success; 400 invalid environment/artifact; 409 when another deployment is in progress.
- **Status** `GET /v2/projects/{projectId}/deployments/{deploymentId}?lastModifiedUtc=`; `deploymentState` ∈ `Created, Pending, Queued, InProgress, Completed, Failed` (terminal: `Completed`, `Failed`). No environment-listing endpoint exists.
- **Cloud deployment type classification**: `Umbraco.Core.dll` at the zip root ⇒ Artifact deployment; else any `.csproj/.sln/.slnx/.fsproj/.vbproj` at any depth ⇒ Git deployment; else undetected. v2 does not enforce a match, so the CLI is the only guard.
- **Git deployment zips** replace the Cloud repository contents (except `.git`), are staged honouring the zip's own `.gitignore`, and require a `.umbraco` file at the root unless `skipPreserveUmbracoCloudJson` is set. The deployed `umbraco-cloud.json` is replaced by the Cloud repository's copy unless that flag is set.
- **Artifact deployment zips** are published output extracted into wwwroot as-is.
- **Umbraco Deploy On-Premises** (v13–v17 identical): `Umbraco:Deploy:Settings:ApiSecret` (Base64, ≥ 64 bytes when no ApiKey is set), `Umbraco:Deploy:Project:CurrentWorkspaceName`, `Umbraco:Deploy:Project:Workspaces[] {Id (GUID), Name, Type, Url}`. Setting the obsolete `ApiKey` overwrites `ApiSecret` with its UTF-8 bytes, so a present ApiKey wins.
- **Umbraco Deploy Cloud** (v13–v17): reads `umbraco-cloud.json` plus `Umbraco:Cloud:`-prefixed environment variables; keys `Deploy:Settings:ApiKey`, `Deploy:Settings:ApiSecret` (Base64, applied after ApiKey), `Deploy:Project {Id, Name, Alias, Workspaces[]}`. On Cloud, v17 topology app settings (`UMBRACO__CLOUD__PROJECT__*`, `UMBRACO__CLOUD__ENVIRONMENTS__*`) override the file's Project/Workspaces. Umbraco Cloud git-based projects still read `Deploy.Settings.ApiKey`.

## Architecture

Single assembly, System.CommandLine 2.x, `Microsoft.Extensions.DependencyInjection` (no Generic Host), typed `HttpClient` via `Microsoft.Extensions.Http`, System.Text.Json source generation, Spectre.Console for progress and the current-environment prompt only. Target `net10.0`; requires .NET SDK 10 on the machine. Code kept AOT/trim-compatible (`IsAotCompatible=true`, analyzers on in CI) so Native AOT tool packages can be enabled later without code changes. No plugin model; each command group lives in its own namespace so it can later be split out.

```
Umbraco.Cli.slnx
version.json                              Nerdbank.GitVersioning
Directory.Build.props / Directory.Packages.props   central package management; NBGV as GlobalPackageReference
src/Umbraco.Cli/
  Program.cs                              entry point, Ctrl+C handling
  CliJsonContext.cs                       source-generated JSON for --output json results
  Infrastructure/
    CliApplication.cs, ServiceRegistration.cs, ErrorHandler.cs   root command, DI container, exception → exit code mapping
    Output/                               IOutput (stdout, text|json), IReporter/IStatusSink (Spectre live | plain lines), IPrompter, terminal/CI detection
    Json/                                 JsonDocumentEditor (comment- and order-preserving edits)
    Processes/                            IGit (ls-files, HEAD subject), IDotnetCli (--version, package list)
  Cloud/
    CloudCommands.cs                      umbraco cloud deployments {upload, deploy, show}
    Api/                                  CloudApiClient (v2), DTOs, CloudJsonContext, CloudApiException, ProgressStreamContent, poll retry policy
    Artifacts/                            ArtifactClassifier, ArtifactZipBuilder, ArtifactUploader
    DeploymentWaiter.cs                   polling loop shared by deploy and show --wait
  Deploy/
    DeployCommands.cs                     umbraco deploy init
    ProjectDetection/                     locate csproj, detect Umbraco Deploy package via IDotnetCli
    Configuration/                        EnvironmentSpec parsing, ApiSecret (64 random bytes → Base64), OnPremisesConfigurationWriter, CloudConfigurationWriter
tests/Umbraco.Cli.Tests/                  NUnit, Moq; mirrors src/
.github/workflows/                        build.yml, release.yml, prepare-release.yml, prepare-patch-release.yml, merge-release.yml
```

Unit boundaries: `CloudApiClient` knows nothing about the CLI; `ArtifactClassifier` is a pure function over relative paths; `JsonDocumentEditor` knows nothing about Umbraco Deploy; the configuration writers translate Umbraco Deploy rules into editor operations; command classes only bind options, call services, and map results to output and exit codes.

### JsonDocumentEditor

System.Text.Json drops comments on write, so the editor never re-serialises a whole file. It tokenises with `Utf8JsonReader` (`JsonCommentHandling.Allow`, trailing commas allowed) to locate the byte spans of target properties and objects, then splices text: replace a value, replace an array, or insert a missing property/section. Inserted text uses the file's detected indentation, line endings, and BOM. Everything outside the edited spans — comments, `$schema`, property order, whitespace — stays byte-for-byte identical. A missing file is created as `{}` first. Writes go to a temp file in the same directory, then atomically replace the original.

## Global conventions

- **Output**: `-o|--output text|json` (default `text`). Results go to stdout; in `json` mode stdout contains exactly one JSON object (camelCase, source-generated). JSON field names are public contract from 0.1.0.
- **Progress and diagnostics** go to stderr via `IProgressReporter`:
  - Live mode: Spectre.Console `IAnsiConsole` bound to `Console.Error`. Upload shows a byte progress bar (rate, ETA) then an "Awaiting response…" spinner; waiting shows a spinner with elapsed time and status messages printed above it. OSC 9;4 taskbar progress is emitted only when `WT_SESSION` or ConEmu is detected, and cleared on exit.
  - Plain mode (timestamped lines, upload progress at most every 10 %) whenever stderr is redirected, `CI`, `TF_BUILD` or `GITHUB_ACTIONS` is set, or `--no-progress` is passed.
  - `NO_COLOR` is honoured. Warnings/errors are `warning:`/`error:` lines with an optional `hint:` line.
- **Verbose**: `--verbose` adds HTTP method/path/status and stack traces for unexpected errors. Headers are never logged; the CI/CD API key and ApiSecrets are always masked.
- **Exit codes**:

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | General/unexpected error, Cloud API 400/404/409/5xx, missing `git`/`dotnet`, SDK < 10 |
| 2 | Invalid usage or validation error (including missing credentials) |
| 3 | Deployment reached `Failed` |
| 4 | Wait timeout elapsed (the Deployment continues) |
| 5 | Authentication failed (Cloud API 401) |
| 130 | Cancelled with Ctrl+C (the Deployment continues) |

- **No telemetry** in 0.1.0.

## `umbraco cloud deployments`

Shared options: `--project-id` (env `UMBRACO_CLOUD_PROJECT_ID`), `--api-key` (env `UMBRACO_CLOUD_API_KEY`), `-o`, `--no-progress`, `--verbose`. A missing value exits 2, naming both the flag and the environment variable. Credentials are never persisted. The API base URL can be overridden only via the undocumented environment variable `UMBRACO_CLOUD_API_URL` (for testing).

### `upload`

Options: exactly one of `--file <zip>` or `--path <dir>`; `--type git|artifact` (overrides classification; only valid with `--path`, otherwise exit 2); `--description`; `--version`. Choice values (`--type`, `-o`) are lowercase and case-sensitive.

1. `--path`: classify the directory with `ArtifactClassifier` (same rules as Umbraco Cloud; `--type` overrides; undetected ⇒ exit 2 suggesting `--type`).
   - **Artifact deployment**: zip every file under the directory, relative paths, no ignore rules; `.git/` directories and symbolic links are skipped.
   - **Git deployment**: file list from `git ls-files --cached --others --exclude-standard -z` run in the directory (exit 1 if `git` is missing or the directory is not in a work tree); `.git` always excluded; `.gitignore` included; symbolic links and files deleted from the work tree are skipped. Missing `.umbraco` at the root: `upload` warns (whether it matters is decided at deploy time) with a hint that the Deployment will need `--skip-preserve-umbraco-cloud-json`; `deploy --path` fails with exit 2 before uploading unless `--skip-preserve-umbraco-cloud-json` is given.
2. `--file`: used as-is; must have a `.zip` extension.
3. Zips larger than 256 MiB ⇒ exit 2 before uploading.
4. Upload via `ProgressStreamContent` (streams the file, reports bytes, implements `TryComputeLength` so the request is not chunked). `HttpClient.Timeout` is infinite; cancellation is by token.
5. Output: text `Artifact <artifactId> uploaded (<size>)`; json the upload response. Temporary zips are deleted.

### `deploy`

Options: exactly one of `--artifact-id`, `--file`, `--path` (the last two run `upload` first, accepting `--type`, `--description`, `--version`); `--environment <alias>` (required); `--message`; `--no-build-and-restore`; `--skip-version-check`; `--skip-preserve-umbraco-cloud-json`; `--no-schema-extraction`; `--no-wait`; `--timeout <duration>` (default 15 min).

1. Commit message: `--message`, else the git `HEAD` subject of the current directory, else `Deployed with Umbraco CLI`.
2. `POST …/deployments` → `deploymentId`. 409 ⇒ exit 1, "another deployment is in progress". Never retried automatically.
3. Unless `--no-wait`, run `DeploymentWaiter`: first poll immediately, then every 25 s, passing `lastModifiedUtc` of the last seen message and printing only new status messages. `Completed` ⇒ 0; `Failed` ⇒ print final messages, 3; timeout ⇒ 4 with the `deploymentId` and a `show <id> --wait` hint; Ctrl+C ⇒ 130, "Stopped waiting; the deployment continues on Umbraco Cloud".
4. Output: text summary line with state and duration; json the final deployment object, plus `artifactId` when an upload was performed.

### `show <deploymentId>`

Single `GET`, prints the Deployment. `--wait` and `--timeout` run `DeploymentWaiter` with the same exit codes as `deploy`.

### Retry policy

Only status-poll `GET`s are retried: on 429, 5xx, and network errors, exponential backoff from 5 s capped at the 25 s poll interval, honouring `Retry-After` up to 60 s, giving up after 5 consecutive failures (exit 1). Uploads and deployment starts are never retried. Start and poll requests time out after 100 s (uploads have no timeout); a start timeout exits 1 with a note that the deployment may have started and a hint to check the Umbraco Cloud Portal.

## `umbraco deploy init`

Supports every Umbraco Deploy version that has `ApiSecret` configuration (no version check). Requires .NET SDK 10.

Options: `--project <csproj|dir>`; `--environment "Name=…;Type=…;Url=…[;Id=…]"` (repeatable); `--current-environment <name>` (Umbraco Deploy On-Premises only); `--project-id <guid>` (Umbraco Deploy Cloud only; also reads `UMBRACO_CLOUD_PROJECT_ID`); `--rotate-secret`; `--dry-run`; `--non-interactive`; `-o`.

1. **Locate the project**: `--project`, else the single `*.csproj` in the current directory; zero or several ⇒ exit 2 listing candidates.
2. **Check the SDK**: `dotnet --version` run in the project directory (respecting `global.json`); major < 10 ⇒ exit 1, ".NET SDK 10 or later is required (found X, possibly pinned by global.json)".
3. **Detect the Umbraco Deploy package**: `dotnet package list --project "<csproj>" --include-transitive --format json` (restores by default; `--interactive` passed only when stdin and stderr are terminals and `--non-interactive` isn't set). Search top-level and transitive packages of all frameworks for `Umbraco.Deploy.OnPrem` / `Umbraco.Deploy.Cloud`. Both or neither ⇒ exit 2. Command failure ⇒ exit 1 with its stderr. The resolved version is reported, not checked. Projects referencing Umbraco Deploy through a `ProjectReference` are not detected (out of scope).
4. **Validate input**: each `--environment` needs `Name`, `Type` (lowercased; `development`, `staging`, `live` or any other lowercase value), absolute `Url` (HTTPS unless the host is a loopback address); optional `Id` must be a non-empty GUID; names unique (case-insensitive). `--current-environment` must match a given name (or an existing Workspace name when no `--environment` is given) and is an error for Cloud; `--project-id` is an error for On-Premises.
5. **Prompt when needed**: only if stdin and stderr are terminals and `--non-interactive` is not set, prompt for missing required values; for On-Premises with Environments given but no `--current-environment`, prompt for which Environment this site is. Non-interactive with missing values ⇒ exit 2.
6. **Plan changes** against the loaded settings file:

   Environments (both packages):
   - No `--environment` given ⇒ the `Workspaces` list is not touched.
   - Otherwise the list is replaced by the given Environments, in the given order. An entry whose name matches an existing entry (case-insensitive) reuses that entry's `Id` unless `Id=` is given; other entries get a new GUID.

   Umbraco Deploy On-Premises (`appsettings.json` next to the csproj, section `Umbraco:Deploy`):

   | Existing state | Without `--rotate-secret` | With `--rotate-secret` |
   |---|---|---|
   | Valid ApiSecret (Base64, ≥ 64 bytes), no ApiKey | keep | generate new |
   | ApiKey set | keep ApiKey, warn that it overrides any ApiSecret | remove ApiKey, generate ApiSecret |
   | No/invalid ApiSecret, no ApiKey | generate | generate |

   `CurrentWorkspaceName` is set from `--current-environment` when given.

   Umbraco Deploy Cloud (`umbraco-cloud.json` next to the csproj, section `Deploy`):

   | Existing state | Without `--rotate-secret` | With `--rotate-secret` |
   |---|---|---|
   | ApiKey set | write no secret; explain (ADR 0002) | same, and warn that rotation is not possible while an ApiKey is set |
   | Valid ApiSecret, no ApiKey | keep | generate new |
   | No/invalid ApiSecret, no ApiKey | generate | generate |

   `Deploy.Project.Id` is set from `--project-id` only when absent; a different existing value is kept with a warning. No current Environment is written for Cloud.

7. **Apply**: `--dry-run` prints the planned operations with secrets masked and writes nothing. Otherwise apply through `JsonDocumentEditor`. When the resulting content is byte-for-byte identical to the existing file, nothing is written and the file is reported as already up to date.
8. **Report**: text summary of changes. A newly generated ApiSecret is printed in full with a note that every Environment must use the same value (ADR 0001). For Cloud, add that the file reaches Umbraco Cloud only through a Deployment with `--skip-preserve-umbraco-cloud-json`. JSON output (public contract, `InitResult`): `{ package, packageVersion, file, dryRun, environments[{ id, name, type, url }], changes[], apiSecretGenerated, apiSecret? }` (`apiSecret` only when newly generated, and omitted for a dry run).

## Testing

Unit tests in `tests/Umbraco.Cli.Tests` (NUnit 3 and Moq, aligned with Umbraco CMS; run with Microsoft.Testing.Platform like Umbraco Deploy; `Microsoft.Extensions.TimeProvider.Testing` for time), mirroring `src/`:

- `ArtifactClassifier`: root `Umbraco.Core.dll`, nested project/solution files, `./` prefix, case variations, undetected.
- `ArtifactZipBuilder` (temp directories, fake `IGit`): Git deployment zips exactly the listed files, never `.git`, always `.gitignore`; Artifact deployment zips everything; detects a missing `.umbraco`.
- `ArtifactUploader`: size limit (the `.umbraco` warning on `upload` versus failure on `deploy --path` is covered by the command tests).
- `CloudApiClient` (fake `HttpMessageHandler`): paths, `Umbraco-Cloud-Api-Key` header, multipart field names, request bodies, state mapping, ProblemDetails mapping, retries on poll `GET`s only.
- `DeploymentWaiter` (fake `TimeProvider`): Completed, Failed, timeout, `lastModifiedUtc` progression, cancellation.
- `JsonDocumentEditor` (inline expected documents): comments, `$schema`, order, indentation, CRLF/LF, BOM preserved byte-for-byte; missing-section insertion; array replacement; `{}` creation.
- Configuration writers (inline expected documents): every row of both decision tables; `Id` reuse; `Project.Id` only when absent.
- Package detection: JSON in the shape of `dotnet package list` version 1 output; SDK version parsing including `global.json`-pinned older versions.
- Commands: end-to-end through System.CommandLine with faked services, asserting stdout, stderr and exit codes for main paths in text and json.

Integration tests (`[Category("Integration")]`): real `git ls-files` on a temp repository; real `dotnet --version` and `dotnet package list` on a minimal generated project without packages (restores offline). Cloud smoke tests (`[Category("Smoke")]`) run only when `UMBRACO_CLOUD_PROJECT_ID` and `UMBRACO_CLOUD_API_KEY` are set (the deployment test also needs `UMBRACO_CLOUD_TEST_ARTIFACT` and `UMBRACO_CLOUD_TEST_ENVIRONMENT`; optional `UMBRACO_CLOUD_API_URL`); excluded in CI.

## Build, versioning and release

Mirrors `umbraco/Umbraco-Deploy`, with GitHub Actions replacing its Azure Pipelines build.

- **Versioning**: Nerdbank.GitVersioning via `GlobalPackageReference`. `version.json` copies Deploy's settings (`publicReleaseRefSpec: ["^refs/tags/release-"]`, `release.tagName: "release-{version}"`, `release.branchName: "release/{version}"`, SemVer 2 package versions, `cloudBuild.setAllVariables`) and starts at `"version": "0.1.0-alpha"`. First stable release: tag `release-0.1.0`.
- **Branches**: `develop` (default), `release/{version}`, `main` (released commits only), as in Deploy.
- **Workflows**:
  - `prepare-release.yml`, `prepare-patch-release.yml`, `merge-release.yml` — copied from Umbraco Deploy unchanged.
  - `build.yml` — on PRs to `develop`, `v*/dev`, `release/*` and pushes to those plus `main`, `v*/main`; `fetch-depth: 0`; Ubuntu and Windows; `dotnet build -warnaserror`, `dotnet test` (excluding the Cloud smoke test), AOT/trim analysis, `dotnet pack` with the NBGV version, `.nupkg` uploaded as a build artifact. An `sbom` job (as Deploy's `generate-sbom.yml`) runs on pushes to `develop`/`v*/dev`, or on `workflow_dispatch` with input `sbom: auto|force|skip` (`auto` on those branches only): `npx @cyclonedx/cdxgen --recurse --spec-version 1.6` with project group `Umbraco`, name `Umbraco.Cli`, version `v{NBGV major}`, uploaded to Dependency-Track (URL as repository variable, API key as secret; skipped with a notice when either is missing) and kept as a `sbom` artifact.
  - `release.yml` — when a GitHub release is published (which creates its `release-*` tag; also runnable manually from the tag ref, so nbgv sees the tag); verifies `NBGV_PublicRelease` is true and the package version equals the tag's version; requires approval of the `release` environment; publishes to nuget.org with trusted publishing (`NuGet/login`, OIDC, no API key secret).
- **Package**: `PackageId=Umbraco.Cli` (also the assembly name; help shows the tool command `umbraco`), `PackAsTool=true`, `ToolCommandName=umbraco`, `PackageLicenseExpression=MIT`, README with prerequisites (.NET SDK 10) and quick starts for both command groups.
- **Repository files**: `LICENSE` (MIT), `README.md`, `GLOSSARY.md`, `docs/adr/`, `.editorconfig` and `.globalconfig` (from Umbraco Deploy), `.gitattributes`, `.gitignore`, `nuget.config`, `global.json`.

## Out of scope for 0.1.0

- `cloud deployments list`, `diff`, `artifacts list`.
- v3 direct-to-blob upload for Artifacts over 256 MiB.
- `umbraco cloud login` and persisted profiles.
- Telemetry.
- Native AOT tool packages.
- Detecting Umbraco Deploy referenced via `ProjectReference`.
- Validating `--environment` aliases against Umbraco Cloud (no API exists).

## Open items

- Confirm with the Cloud team that nothing on Umbraco Cloud breaks when a Cloud project without an ApiKey has an ApiSecret in `umbraco-cloud.json` (ADR 0002 avoids the ApiKey case).

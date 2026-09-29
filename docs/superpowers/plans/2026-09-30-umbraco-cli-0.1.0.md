# Umbraco CLI 0.1.0 Implementation Plan

> **For agentic workers:** this plan was executed as a proof of concept on the `feature/initial-cli` branch (the user asked for a working POC overnight instead of a plan-first review). Completed tasks are checked; the code in the repository is the source of truth. Remaining work is listed under [Follow-ups](#follow-ups); use superpowers:executing-plans for them.

**Goal:** Ship `Umbraco.Cli` 0.1.0, a .NET global tool (`umbraco`) with `cloud deployments upload|deploy|show` and `deploy init`, as designed in [the spec](../specs/2026-09-30-umbraco-cli-design.md).

**Architecture:** One assembly. System.CommandLine 2 defines the commands under a plain `Command("umbraco")` root (so help shows the tool command, not the assembly name); each command binds its options to a settings record and calls a handler resolved from a per-invocation `ServiceProvider` (Microsoft.Extensions.DependencyInjection, no Generic Host). Handlers use small services behind interfaces (`ICloudApiClient`, `IGit`, `IDotnetCli`, `IReporter`, `IOutput`, `IPrompter`, `IDeployConfigurationWriter`) so every command is testable in-process through `CliApplication`.

**Tech Stack:** .NET 10, System.CommandLine 2.0.12, System.Text.Json source generation, Microsoft.Extensions.Http, Spectre.Console 0.57.2 (progress and the current-environment prompt), NUnit 3.14 + Moq 4.21 on Microsoft.Testing.Platform, Nerdbank.GitVersioning, StyleCop.Analyzers + Umbraco.Code (as in Umbraco Deploy).

## Global Constraints

- Target `net10.0`; requires .NET SDK 10 (`global.json` rolls forward from 10.0.100).
- Keep the code trim/AOT-compatible: `IsAotCompatible=true`, source-generated JSON only, no reflection-based serialization; CI publishes trimmed with `-warnaserror`.
- `dotnet build -warnaserror` must pass (Deploy's `.globalconfig`, `.editorconfig` and test `codeanalysis.ruleset` apply).
- All types are `internal`; the test project has `InternalsVisibleTo`.
- Results go to stdout (`-o text|json`, lowercase), progress and diagnostics to stderr; exit codes 0/1/2/3/4/5/130 as in the spec.
- Never log the CI/CD API key or an ApiSecret (except printing a newly generated ApiSecret as the command result).
- Commit messages: single-line, imperative, no attribution trailers.

---

### Task 1: Repository scaffolding — done

**Files:** `Umbraco.Cli.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `tests/Directory.Build.props`, `tests/Directory.Packages.props`, `tests/codeanalysis.ruleset`, `.editorconfig` and `.globalconfig` (copied from Umbraco Deploy), `global.json`, `nuget.config` (nuget.org only, with source mapping), `version.json` (NBGV, `0.1.0-alpha`, `release-{version}` tags), `.gitattributes`, `.gitignore`, `LICENSE`, `src/Umbraco.Cli/Umbraco.Cli.csproj` (assembly and `PackageId` `Umbraco.Cli`, `PackAsTool`, `ToolCommandName=umbraco`), `tests/Umbraco.Cli.Tests/Umbraco.Cli.Tests.csproj`.

- [x] Build with zero warnings: `dotnet build -warnaserror`

### Task 2: Command host, output and error handling — done

**Files:** `src/Umbraco.Cli/Program.cs`, `Infrastructure/{CliApplication,GlobalOptions,GlobalSettings,ServiceRegistration,ErrorHandler,ExitCodes,CliException,UsageException,IEnvironment,SystemEnvironment,Durations,AtomicFile}.cs`, `Infrastructure/Output/*`, `CliJsonContext.cs`. **Tests:** `Infrastructure/{CliApplicationTests,DurationsTests,PlainReporterTests,LiveReporterTests,TerminalDetectionTests}.cs`, `TestHelpers/*`.

- [x] Parse errors → exit 2; `CliException` → its exit code; `OperationCanceledException` → 130; unexpected → 1 (stack trace only with `--verbose`)
- [x] `PlainReporter` (timestamped lines, 10% upload steps) and `LiveReporter` (Spectre progress/status on stderr, OSC 9;4 taskbar progress in Windows Terminal/ConEmu), chosen by `TerminalDetection`
- [x] Ctrl+C handled by `Program` (System.CommandLine's handler disabled) so a wait can report that the deployment continues; a second Ctrl+C terminates the process
- [x] Help shows `umbraco` (tests `Help_ListsCommandGroups`, `SubcommandHelp_UsesTheToolCommandName`)
- [x] `AtomicFile` keeps the Unix file mode and writes through symbolic links

### Task 3: Comment-preserving JSON editor — done

**Files:** `Infrastructure/Json/JsonDocumentEditor.cs`. **Tests:** `Infrastructure/JsonDocumentEditorTests.cs`.

- [x] Get/set/remove by case-insensitive path; splices text so comments, `$schema`, order, indentation (spaces/tabs), CRLF/LF and a BOM are preserved (also around removed properties); relaxed escaping keeps Base64 readable

### Task 4: External processes — done

**Files:** `Infrastructure/Processes/*`. **Tests:** `Infrastructure/GitTests.cs`, `Infrastructure/ProcessIntegrationTests.cs` (`[Category("Integration")]`, real git and dotnet).

- [x] `git ls-files --cached --others --exclude-standard -z`, `git log -1 --format=%s`
- [x] `dotnet --version` (SDK ≥ 10 check), `dotnet package list --project … --include-transitive --format json`

### Task 5: Umbraco Cloud API client — done

**Files:** `Cloud/Api/*`. **Tests:** `Cloud/CloudApiClientTests.cs`.

- [x] v2 upload (multipart `file`/`description`/`version`, streamed by `ProgressStreamContent` with byte progress and a known length), start, get status (`lastModifiedUtc`)
- [x] Only status polls are retried (429/5xx/network, 5 s doubling to 25 s, `Retry-After` capped at 60 s, 5 attempts); 401 → exit 5; ProblemDetails in messages
- [x] Start and poll requests time out after 100 s; a start timeout → exit 1, not retried (the deployment may have started)
- [x] Response DTOs use settable properties (the source generator sets missing init-only properties to null)

### Task 6: Artifacts and waiting — done

**Files:** `Cloud/Artifacts/*`, `Cloud/DeploymentWaiter.cs`. **Tests:** `Cloud/{ArtifactClassifierTests,ArtifactZipBuilderTests,ArtifactUploaderTests,DeploymentWaiterTests}.cs`.

- [x] Classifier mirrors Umbraco Cloud's deployment type detection; Git deployments zip the git file list (never `.git`, always `.gitignore`), Artifact deployments zip everything; symbolic links and `.git/` are skipped for both types
- [x] 256 MiB API limit (`ArtifactUploader.DefaultMaxArtifactSize`; Cloudflare allows 500 MB); `.umbraco` warning on upload, error on `deploy --path` unless `--skip-preserve-umbraco-cloud-json`
- [x] Poll every 25 s, print each status message once, timeout → exit 4, cancel → warning + 130

### Task 7: `umbraco cloud deployments upload|deploy|show` — done

**Files:** `Cloud/CloudCommands.cs`, `Cloud/*CommandHandler.cs`, `Cloud/*CommandSettings.cs`, `Cloud/{CloudCredentialsResolver,DeploymentResultWriter}.cs`. **Tests:** `Cloud/CloudCommandsTests.cs`, `Cloud/CloudSmokeTests.cs` (`[Category("Smoke")]`, opt-in).

- [x] Credentials from options or `UMBRACO_CLOUD_PROJECT_ID`/`UMBRACO_CLOUD_API_KEY`; base URL override via `UMBRACO_CLOUD_API_URL`
- [x] Commit message: `--message`, else git `HEAD` subject, else `Deployed with Umbraco CLI`
- [x] Verified against the real API with invalid credentials (401 → exit 5 for `show` and `upload`)

### Task 8: `umbraco deploy init` — done

**Files:** `Deploy/*`. **Tests:** `Deploy/*Tests.cs`.

- [x] Project location, SDK check, package detection via `dotnet package list`
- [x] On-Premises (`appsettings.json`) and Cloud (`umbraco-cloud.json`) writers implement the spec's decision tables; environment IDs reused by name; unchanged files are not rewritten
- [x] Interactive current-environment selection (cancellable) only when stdin and stderr are terminals; `--dry-run` masks the secret
- [x] Verified end-to-end on a real site referencing `Umbraco.Deploy.OnPrem` 17.3.1 (first run writes, second run reports "already up to date", `--rotate-secret` replaces the secret)

### Task 9: Build, SBOM and release workflows — done

**Files:** `.github/workflows/{build,release}.yml`; `prepare-release.yml`, `prepare-patch-release.yml`, `merge-release.yml` copied unchanged from Umbraco Deploy.

- [x] The build, test, trimmed publish and pack commands of `build.yml` pass locally
- [x] SBOM with cdxgen to Dependency-Track on pushes to `develop`/`v*/dev` or a manual run (skips the upload when `DEPENDENCY_TRACK_URL`/`DEPENDENCY_TRACK_API_KEY` aren't configured)
- [x] Release: triggered by publishing a GitHub release, or run manually from the tag ref; public-release and version checks, `release` environment approval, NuGet trusted publishing (`vars.NUGET_USER`)

### Task 10: Documentation and packaging — done

- [x] `README.md` (packed into the NuGet package), `GLOSSARY.md`, ADRs 0001–0002
- [x] `dotnet pack` → installed with `dotnet tool install --tool-path` and ran `umbraco --help`

## Verification

The Integration tests need `git` and the .NET SDK 10 on the PATH; the Smoke tests are skipped without Umbraco Cloud credentials.

```sh
dotnet build -warnaserror                        # 0 warnings
dotnet test --filter "TestCategory!=Smoke"       # all passed
dotnet publish src/Umbraco.Cli -c Release -r linux-x64 --self-contained -p:PublishTrimmed=true -p:PackAsTool=false -warnaserror -o artifacts/trimmed
dotnet pack src/Umbraco.Cli -c Release -o artifacts/packages
```

## Follow-ups

- [ ] **Real deployment.** With a Cloud project and a valid artifact for it, run the smoke tests and expect 2 passed, 0 skipped:

  ```sh
  UMBRACO_CLOUD_PROJECT_ID=<id> UMBRACO_CLOUD_API_KEY=<key> UMBRACO_CLOUD_TEST_ARTIFACT=<site.zip> UMBRACO_CLOUD_TEST_ENVIRONMENT=<alias> dotnet test --filter "TestCategory=Smoke"
  ```

  Then, from a clone of the project's Cloud repository (with its `.umbraco` file), deploy source code and follow a deployment:

  ```sh
  dotnet run --project src/Umbraco.Cli -- cloud deployments deploy --path <clone> -e <alias>
  dotnet run --project src/Umbraco.Cli -- cloud deployments deploy --path <clone> -e <alias> --no-wait -o json
  dotnet run --project src/Umbraco.Cli -- cloud deployments show <deploymentId> --wait
  ```

  Expect exit 0 and a `Completed` state; check that the uploaded zip contained no `bin/`/`obj/` (the clone's `.gitignore` applied).
- [ ] **Live progress in a real terminal.** Run the `deploy --path` command above in Windows Terminal and in a Linux/macOS terminal: the upload shows a progress bar (bytes, speed, remaining time) followed by "awaiting response", the wait shows a spinner with the state and elapsed time with status messages printed above it, Windows Terminal shows taskbar progress that is cleared on exit, and `--no-progress` (or piping stderr) switches to plain timestamped lines.
- [ ] **GitHub setup.**
  1. Create `github.com/umbraco/Umbraco.Cli` with default branch `develop`, push `develop` (with this branch merged) and create `main` from the first stable release.
  2. Create the `release` environment with required reviewers.
  3. On nuget.org, add a trusted publishing policy for owner `umbraco`, repository `Umbraco.Cli`, workflow `release.yml`, environment `release`; set the repository variable `NUGET_USER` to the nuget.org account that owns the policy.
  4. Set the repository variable `DEPENDENCY_TRACK_URL` and secret `DEPENDENCY_TRACK_API_KEY` (as used by Umbraco Deploy).
  5. Run "Prepare release" (`rc`) on `develop`, publish the GitHub release `release-0.1.0-rc1`, approve the `release` environment, and check that `Umbraco.Cli 0.1.0-rc1` appears on nuget.org.
- [ ] **Open item from the spec:** confirm with the Cloud team that an ApiSecret in `umbraco-cloud.json` is safe for projects without an ApiKey.

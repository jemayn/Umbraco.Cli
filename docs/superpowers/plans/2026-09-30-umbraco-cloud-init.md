# `umbraco cloud init` Implementation Plan

> **For agentic workers:** this plan records an implementation that was built directly on `develop` (the user asked to start implementing after the design review). Completed tasks are checked; the code is the source of truth. Remaining work is under [Follow-ups](#follow-ups).

**Goal:** Implement `umbraco cloud init` as designed in [the spec](../specs/2026-09-30-umbraco-cloud-init-design.md).

**Architecture:** A command handler (`Cloud/Init/CloudInitCommandHandler`) orchestrates small units: `UmbracoReleases` (versions, aliases, lifecycle), `PackageVersionResolver` (compatible versions from the `Umbraco.*` dependency graph), `PackagePlan` (required packages, Products, Integration packages), `UmbracoProjectCreator` (template, Cloud scaffold, solution) and file writers (`CloudScaffold`, `DotUmbracoFile`, `CloudProjectSettings`, `SolutionFile`). External tools go through `IDotnetCli.RunAsync`, nuget.org through `INuGetClient`.

**Tech Stack:** as in the 0.1.0 plan, plus `NuGet.Versioning` 7.9.0 (ADR 0005).

## Global Constraints

- Umbraco 17 and later only; never run git; never change existing package references, `Program.cs` or `.gitignore`.
- Never print the Umbraco ID client secret (ADR 0003); release types and lifecycle phases are calculated (ADR 0004).
- Stay trim/AOT-compatible (source-generated JSON, no NuGet.Protocol) and warning-free with `-warnaserror`.

---

### Task 1: NuGet client — done

**Files:** `Infrastructure/NuGet/*`. **Tests:** `Infrastructure/NuGetClientTests.cs`, `TestHelpers/FakeNuGetClient.cs`.

- [x] Service index discovery (`RegistrationsBaseUrl/3.6.0`), inlined and paged registrations, gzip (via `SocketsHttpHandler.AutomaticDecompression`), per-run cache, 404 ⇒ empty, retries
- [x] Dependency groups for `net10.0`, falling back to older .NET and .NET Standard

### Task 2: Releases, aliases and lifecycle — done

**Files:** `Releases/{ReleaseType,LifecyclePhase,UmbracoMajor,UmbracoReleases}.cs`. **Tests:** `Releases/*Tests.cs`.

- [x] LTS for 17 and every fourth major; STS 6/9/12 and LTS 6/24/36 months from the first stable `Umbraco.Cms` publish date (matches the published dates of 17 and 18)
- [x] `latest`, `lts`, `rc`, `beta`, `next`/`preview`, major, major.minor, exact; below 17 rejected

### Task 3: Packages — done

**Files:** `Cloud/Init/{catalog.json,ProductCatalog,InitJsonContext,PackagePlan,PlannedPackage,PackageVersionResolver}.cs`, `Infrastructure/Processes/PackageList.cs` (also used by `deploy init`). **Tests:** `Cloud/Init/{PackagePlanTests,PackageVersionResolverTests}.cs`.

- [x] Embedded catalogue with the required packages, Umbraco Deploy (required) and Forms, and the `Umbraco.Forms.Deploy` Integration package
- [x] Newest listed version in the CMS major whose `Umbraco.*` dependency graph accepts the CMS version (NuGet's lowest-match resolution for dependencies)

### Task 4: Files — done

**Files:** `Cloud/Init/{CloudScaffold,DotUmbracoFile,CloudProjectSettings,IdentityValues,SolutionFile,GitignoreHints}.cs`, `Cloud/Init/Scaffold/*.template`. **Tests:** `Cloud/Init/CloudInitFilesTests.cs`.

- [x] Cloud scaffold files (from Umbraco Cloud's 17/18 scaffold) embedded as `.template` resources, README with the project name
- [x] `.umbraco` at the git root, else the solution directory, else the project directory; kept when present
- [x] `umbraco-cloud.json` Identity (placeholders, given values, unchanged) and `UMBRACO-CLOUD` licences via `JsonDocumentEditor`
- [x] Solution Items in the `.slnx` (without a BOM)

### Task 5: Command — done

**Files:** `Cloud/Init/{CloudInitCommand,CloudInitSettings,CloudInitCommandHandler,CloudInitResult,CloudInitPackage,UmbracoProjectCreator}.cs`, `Cloud/CloudCommands.cs`, `Infrastructure/{ServiceRegistration,IEnvironment,SystemEnvironment}.cs`, `Infrastructure/Output/{IPrompter,SpectrePrompter}.cs`, `CliJsonContext.cs`. **Tests:** `Cloud/Init/CloudInitCommandTests.cs`, `Cloud/Init/CloudInitIntegrationTests.cs` (`[Category("Integration")]`).

- [x] New and existing modes, prompts (name, version, Products), end-of-life refusal, restore and build, text and JSON output
- [x] Verified for real: a new Umbraco 17 project with Forms is created, built and boots (HTTP 200, backoffice reachable, no "No ClientId set"); an existing Umbraco 17 project is prepared and a second run changes nothing; the trimmed binary works

## Verification

```sh
dotnet build -warnaserror
dotnet test --filter "TestCategory!=Smoke"          # includes the real cloud init integration test (network)
dotnet publish src/Umbraco.Cli -c Release -r win-x64 --self-contained -p:PublishTrimmed=true -p:PackAsTool=false -warnaserror -o artifacts/trimmed
```

## Follow-ups

- [ ] Try `umbraco cloud init` in a real terminal (the version and Product prompts, live progress).
- [ ] Push a project created by `umbraco cloud init --name` to a new Cloud project (or deploy it with `umbraco cloud deployments deploy --path`) and check that Umbraco Cloud builds it and Umbraco ID login works there.
- [ ] Open item from the spec: confirm with the Umbraco Cloud team that the embedded scaffold files may be redistributed and who reports scaffold changes for new majors.

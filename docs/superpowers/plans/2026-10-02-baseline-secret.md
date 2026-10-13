# `umbraco deploy init` baseline options: Implementation Plan

**Date:** 02-10-2026
**Status:** Implemented. Depends on the Umbraco Deploy release that adds `BaselineUrl`, `BaselineApiSecret` and `ChildApiSecrets` (Deploy 17 and 18; plan `docs/plans/2026-10-02-baseline-secret.md` and ADR 0004 in the Umbraco-Deploy repository).

**Goal:** Let `umbraco deploy init` configure a Baseline connection for Umbraco Deploy On-Premises: the Child project side (`BaselineUrl`, `BaselineApiSecret`) and the Baseline project side (`ChildApiSecrets`). On Umbraco Cloud, provisioning writes these values, so the CLI only reports them.

**Background:** the ApiSecret no longer has to be shared between related projects. Each project has its own ApiSecret (shared by its Environments), and a Child project restores from its Baseline project with a separate Baseline secret per Baseline connection. The Baseline project accepts the secrets in `ChildApiSecrets` (keyed by an opaque name) only for restores.

## Global Constraints

- Only the On-Premises writer (`appsettings.json`, `Umbraco:Deploy:Settings`) writes baseline settings. Using a baseline option with Umbraco Deploy Cloud is a `UsageException` (exit 2) explaining that Cloud provisions them (same reasoning as ADR 0002).
- Never generate a Baseline secret or child secret without an explicit option.
- Never store or log secrets. Print a generated secret in full once, as with the ApiSecret; mask it in a dry run.
- JSON output (`InitResult`) is a public contract: additive fields only.
- Stay trim/AOT-compatible (source-generated JSON) and warning-free with `-warnaserror`.

---

### Task 1: Child project options

**Files:** `Deploy/DeployCommands.cs`, `Deploy/InitCommandSettings.cs`, `Deploy/InitRequest.cs`, `Deploy/Configuration/OnPremisesConfigurationWriter.cs`. **Tests:** `Deploy/OnPremisesConfigurationWriterTests.cs`, `Deploy/InitCommandTests.cs`.

- [x] `--baseline-url <url>` writes `BaselineUrl`. Validate it as an absolute https URL, reusing the URL validation in `EnvironmentSpec`.
- [x] `--baseline-secret <base64>` (`[env: UMBRACO_DEPLOY_BASELINE_SECRET]`) writes `BaselineApiSecret`, validated with `ApiSecret.IsValid` (Base64, at least 64 bytes).
- [x] `--rotate-baseline-secret` generates a new `BaselineApiSecret` (keeps a valid existing one otherwise). Mutually exclusive with `--baseline-secret`.
- [x] `--remove-baseline` removes `BaselineUrl` and `BaselineApiSecret`. Mutually exclusive with the other child options.
- [x] A Baseline secret without a `BaselineUrl` (configured or given) is a warning: it is only used for calls to the Baseline workspace.

### Task 2: Baseline project options

**Files:** as Task 1. **Tests:** as Task 1.

- [x] `--child-secret <name>` generates a `ChildApiSecrets:{name}` entry. An existing valid entry is kept, with a note. To rotate, add `{name}-1`, switch the child, then remove `{name}`.
- [x] `--remove-child-secret <name>` removes that entry (break reference or a leaked secret).
- [x] Output says that the generated value must be set as `BaselineApiSecret` on the Child project, and the reverse for `--rotate-baseline-secret`.

### Task 3: Cloud reporting

**Files:** `Deploy/Configuration/CloudConfigurationWriter.cs`, `Deploy/InitCommandHandler.cs`. **Tests:** `Deploy/CloudConfigurationWriterTests.cs`.

- [x] Report a configured `BaselineUrl` (also the obsolete `Deploy:Project:Upstream`), whether `BaselineApiSecret` is set, and the number of `ChildApiSecrets` entries. Never write them.
- [x] Note that the ApiSecret is now per project and that Cloud provisions the baseline settings.

### Task 4: Version check

**Files:** `Deploy/InitCommandHandler.cs`. **Tests:** `Deploy/InitCommandTests.cs`.

- [x] When a baseline option is used, check the installed `Umbraco.Deploy.OnPrem` version (already detected with `dotnet package list`). Below the first version with baseline support, warn that the settings are ignored.

### Task 5: Output, docs and ADR

**Files:** `Deploy/InitOutcome.cs`, `Deploy/InitResult.cs`, `CliJsonContext.cs`, `README.md`, `GLOSSARY.md`, `docs/adr/0007-*.md`.

- [x] `InitOutcome` gains `GeneratedBaselineSecret` and `GeneratedChildSecret` (with its name).
- [x] `InitResult` gains `baselineUrl?`, `baselineSecretGenerated`, `baselineSecret?`, `childSecretGenerated`, `childSecretName?`, `childSecret?` and the Cloud report fields `baselineSecretConfigured` and `childSecretCount`.
- [x] README: a `deploy init` section on Baseline connections.
- [x] `GLOSSARY.md`: add Baseline project, Child project, Baseline connection and Baseline secret (as defined in the Umbraco-Deploy `GLOSSARY.md`). Change the ApiSecret definition from "shared by all Environments" to "shared by all Environments of one project".
- [x] ADR 0007: the ApiSecret is per project and a Baseline secret per Baseline connection; the CLI writes baseline settings only for On-Premises, because Cloud provisions them. This revises the assumptions in ADR 0001 (one ApiSecret shared by every Environment) and extends ADR 0002 (no Cloud writes) to the baseline settings.

## Out of scope

- `deploy schema` is unaffected: Schema API calls stay signed with the project's ApiSecret.
- Managing Cloud baseline secrets through a Cloud API (after `umbraco cloud login` and a portal re-roll API exist).

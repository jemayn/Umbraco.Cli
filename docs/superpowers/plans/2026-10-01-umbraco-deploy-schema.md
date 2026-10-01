# `umbraco deploy schema deploy|show` Implementation Plan

> **For agentic workers:** this plan records an implementation that was built directly on `develop` after the plan was approved. Completed tasks are checked; the code is the source of truth.

**Goal:** Implement `umbraco deploy schema deploy|show` as designed in [the spec](../specs/2026-10-01-umbraco-deploy-schema-design.md).

**Architecture:** The commands (`Deploy/Schema/SchemaCommands`) map options to settings for two handlers. `SchemaTargetResolver` resolves the Environment URL and `DeployCredentials` from options, environment variables and the configuration files; `DeployApiClient` calls the Schema API with a fresh `DeployBearerToken` per request; `SchemaDeploymentWaiter` polls and restarts once; `SchemaDeploymentResultWriter` writes the result and maps the exit code.

**Tech Stack:** as in the 0.1.0 plan.

## Global Constraints

- Never store or print the ApiSecret or ApiKey; never retry a start on errors.
- Stay trim/AOT-compatible (source-generated JSON) and warning-free with `-warnaserror`.

---

### Task 1: Schema API client — done

**Files:** `Deploy/Schema/Api/*`, `Deploy/Schema/DeployBearerToken.cs`, `Deploy/Schema/DeployCredentials.cs`. **Tests:** `Deploy/Schema/DeployBearerTokenTests.cs`, `Deploy/Schema/DeployApiClientTests.cs`.

- [x] HMAC bearer token matching `TriggerDeploy.ps1` (path base, `//` collapsed)
- [x] Start and status requests with absolute URIs, 100 s request timeout, error mapping (401/403/404/other), network errors and non-JSON responses

### Task 2: Target resolution — done

**Files:** `Deploy/Schema/SchemaTarget*.cs`, `Deploy/Configuration/EnvironmentSpec.cs` (shared URL validation). **Tests:** `Deploy/Schema/SchemaTargetResolverTests.cs`.

- [x] Options ⇒ environment variables ⇒ `umbraco-cloud.json` ⇒ `appsettings.json`; ApiKey wins within a source
- [x] Environment lookup in the Workspaces of both files

### Task 3: Waiting — done

**Files:** `Deploy/Schema/SchemaDeploymentWaiter.cs`. **Tests:** `Deploy/Schema/SchemaDeploymentWaiterTests.cs`.

- [x] 3 s polls, progress messages, 60 s grace for Unknown and transient failures, one restart, timeout and Ctrl+C hints

### Task 4: Commands, output and docs — done

**Files:** `Deploy/Schema/SchemaCommands.cs`, `Schema*Settings.cs`, `Schema*CommandHandler.cs`, `SchemaDeploymentResult*.cs`, `Infrastructure/TimeoutOption.cs` (shared with `cloud deployments`), `CliJsonContext.cs`, `ServiceRegistration.cs`, `README.md`, `GLOSSARY.md`, ADR 0006. **Tests:** `Deploy/Schema/SchemaCommandsTests.cs`.

- [x] `deploy` and `show` with validation (exit 2), text and JSON output, exit codes

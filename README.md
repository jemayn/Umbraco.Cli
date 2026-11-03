# Umbraco CLI

A cross-platform command-line tool for Umbraco products and services in development and CI pipelines. Commands are grouped by product or service:

```
umbraco cloud init
umbraco cloud deployments upload|deploy|show
umbraco deploy init
umbraco deploy schema deploy|show
```

## Installation

Requires the [.NET SDK 10](https://dotnet.microsoft.com/download) or later.

```sh
dotnet tool install --global Umbraco.Cli
umbraco --help
```

## Preparing an Umbraco project for Umbraco Cloud

`umbraco cloud init` adds the Umbraco Cloud packages (`Umbraco.Cloud.Cms`, `Umbraco.Cloud.StorageProviders.AzureBlob` and `Umbraco.Deploy.Cloud`) to an Umbraco project, with versions compatible with its Umbraco CMS version, plus the Umbraco Cloud licence settings and `umbraco-cloud.json`. Run it in the project's directory (or use `--project`); Umbraco 17 and later are supported.

```sh
umbraco cloud init --add forms
```

`--deployment-type` sets how the project is deployed to Umbraco Cloud: `artifact` (default) for published output, or `git` for source code, which also writes the `.umbraco` file Umbraco Cloud builds from (and, for a new project, the Cloud `README.md`).

With `--name`, it first creates a new Umbraco project laid out like a new Cloud project (`src/<name>`, central package management, `.gitignore` files, the Cloud settings and a solution file):

```sh
umbraco cloud init --name MySite --version lts --add forms -- --development-database-type SQLite
```

- `.gitignore`: the .NET one (`dotnet new gitignore`) at the repository root and the template's Umbraco-specific one (with the Umbraco Deploy trigger files) in the project; `--exclude-gitignore` creates neither.
- `--development-mode`: `ide` (default: Production runtime mode, with models as source code, suited to deploying published output) or `backoffice` (as a new Cloud project).
- `--version`: `latest` (default), `lts`, `rc`, `beta`, `next`, a major (`17`), a major.minor (`17.2`) or an exact version. It warns about versions that only receive security fixes and refuses end-of-life versions (unless `--allow-end-of-life`).
- `--add <product>`: add a Product (currently `forms`, which also adds `Umbraco.Forms.Deploy`). Packages that are already referenced are never changed.
- Options after `--` are passed to `dotnet new umbraco`; the Umbraco.Templates version is installed in the CLI's own template store, so your installed templates don't change.
- A new `umbraco-cloud.json` gets placeholder Umbraco ID values, so locally the backoffice login works but Umbraco ID login doesn't (Umbraco Cloud environments get their own values through environment variables). The CLI never writes secrets for Umbraco Cloud: Umbraco Cloud provisions them (see [ADR 0008](docs/adr/0008-umbraco-cloud-secrets-are-provisioned-by-umbraco-cloud.md)). An existing `umbraco-cloud.json` is left unchanged.
- It restores and builds the project afterwards (`--no-build` to skip the build).
- Then optionally run `umbraco deploy init` to configure the environments to transfer content between, and run the project locally once before publishing it, so Umbraco installs and Umbraco Deploy writes the schema (`umbraco/Deploy/Revision`) that is deployed.

## Umbraco Cloud deployments

These commands use the [Umbraco Cloud CI/CD API](https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd/umbraco-cloud-api). Get the project ID and CI/CD API key from the Umbraco Cloud Portal (**Configuration > CI/CD Flow**). The API key is a secret, so it's only read from the `UMBRACO_CLOUD_API_KEY` environment variable (in a pipeline, from its secret store), never from an option, and it's never stored or printed:

| Option | Environment variable |
|---|---|
| `--project-id` | `UMBRACO_CLOUD_PROJECT_ID` |
| | `UMBRACO_CLOUD_API_KEY` |

When neither gives the project ID, it's read from `Deploy:Project:Id` in `umbraco-cloud.json` (`umbraco deploy init --project-id` sets it): `--config` points at the file or a folder containing it, and defaults to the uploaded `--path` (at its root for published output, or in the Umbraco project that its `.umbraco` file points at for source code). For example, to deploy an uploaded artifact: `umbraco cloud deployments deploy --artifact-id <id> --environment live --config publish`.

Deploy source code (a Git deployment) or published output (an Artifact deployment) in one step, waiting until the deployment completes or fails:

```sh
umbraco cloud deployments deploy --path . --environment dev
```

Or upload first and deploy later:

```sh
artifactId=$(umbraco cloud deployments upload --file site.zip -o json | jq -r .artifactId)
umbraco cloud deployments deploy --artifact-id "$artifactId" --environment stage --no-wait
umbraco cloud deployments show <deployment-id> --wait
```

- `--path` zips a directory. The deployment type is detected like Umbraco Cloud does: `Umbraco.Core.dll` at the root means published output (all files are included), a project or solution file means source code (the files git would commit are included, so `.gitignore` is honoured). Use `--type git|artifact` to override.
- Some files are never deployed, also when git would commit them: environment files (`.env`, `.env.*`), local SQLite databases (`*.sqlite.db`), and Umbraco's logs and temporary files (`umbraco/Logs`, `umbraco/Data/TEMP`). The files left out are listed.
- A Git deployment needs the `.umbraco` file of your Umbraco Cloud repository at its root, unless you deploy with `--skip-preserve-umbraco-cloud-json`.
- Artifacts can be at most 256 MiB.
- The commit message defaults to the subject of the current git `HEAD` commit; use `--message` to set it.
- The target must be the left-most or a flexible environment, unless the Cloud project allows deploying to any target (artifact deployments always do). The result shows the artifact ID, so the same artifact can be deployed to another environment with `--artifact-id`:

  ```sh
  umbraco cloud deployments deploy --path publish --type artifact --environment dev
  umbraco cloud deployments deploy --artifact-id <artifact-id> --environment live
  ```
- Other options: `--no-build-and-restore`, `--skip-version-check`, `--skip-preserve-umbraco-cloud-json`, `--no-schema-extraction`, `--timeout <duration>` (default `15m`).

## Umbraco Deploy configuration

`umbraco deploy init` adds the environments of a site and, for Umbraco Deploy On-Premises, ensures an ApiSecret is configured. Run it in the site's project directory (or use `--project`):

```sh
umbraco deploy init \
  --environment "Name=Local;Type=development;Url=https://localhost:44307" \
  --environment "Name=Live;Type=live;Url=https://www.example.com" \
  --current-environment Local
```

- **Umbraco Deploy On-Premises** (`Umbraco.Deploy.OnPrem`): writes `Umbraco:Deploy:Settings:ApiSecret` and `Umbraco:Deploy:Project` in `appsettings.json`.
- **Umbraco Deploy Cloud** (`Umbraco.Deploy.Cloud`): writes `Deploy:Project:Workspaces` in `umbraco-cloud.json`, and never an ApiSecret or ApiKey: Umbraco Cloud provisions them (see [ADR 0008](docs/adr/0008-umbraco-cloud-secrets-are-provisioned-by-umbraco-cloud.md)). Use `--project-id` to set the project ID when it isn't configured yet.

The package is detected with `dotnet package list`. On-Premises, a valid existing ApiSecret is kept (use `--rotate-secret` to replace it) and a newly generated one is written to `appsettings.json`: every environment must be configured with the same value. Generated secrets are never printed (also not with `-o json`), so they can't end up in CI/CD logs; read them from the settings file. `--environment` replaces the configured list, keeping the IDs of environments with the same name. Comments and formatting of the settings file are preserved. Use `--dry-run` to see the changes without writing them.

### Baseline connections

A Child project restores content from its Baseline project with a Baseline secret of its own, separate from the ApiSecret of either project. On Umbraco Deploy On-Premises, `deploy init` configures both sides of a Baseline connection; Umbraco Cloud provisions these settings itself, so for Umbraco Deploy Cloud the options are refused and `deploy init` only reports what is configured (`Deploy:Settings:BaselineUrl` or the obsolete `Deploy:Project:Upstream`, `BaselineApiSecret` and the number of `ChildApiSecrets`).

On the Baseline project (its Live environment), generate a secret for a Child project (it's written to `ChildApiSecrets:<name>` in `appsettings.json`, not printed):

```sh
umbraco deploy init --child-secret customer-a
```

On the Child project, set the Baseline project's URL and the secret:

```sh
UMBRACO_DEPLOY_BASELINE_SECRET="<the child secret>" umbraco deploy init --baseline-url https://baseline.example.com
```

| Option | Applies to | Meaning |
|---|---|---|
| `--baseline-url <url>` | Child project | Sets `BaselineUrl`, the absolute HTTPS URL of the Baseline project's Live environment (HTTP only for localhost, and without a user name or password) |
| `UMBRACO_DEPLOY_BASELINE_SECRET` (environment variable) | Child project | Sets `BaselineApiSecret` (Base64, at least 64 bytes). Only used with `--baseline-url` or a configured `BaselineUrl` (On-Premises) |
| `--rotate-baseline-secret` | Child project | Generates a new `BaselineApiSecret`, to be added to `ChildApiSecrets` on the Baseline project |
| `--remove-baseline` | Child project | Removes `BaselineUrl` and `BaselineApiSecret` (cannot be combined with the other Child project options) |
| `--child-secret <name>` | Baseline project | Generates `ChildApiSecrets:<name>`, to be set as `BaselineApiSecret` on the Child project. A valid existing entry is kept |
| `--remove-child-secret <name>` | Baseline project | Removes that `ChildApiSecrets` entry |

- A secret is never generated without `--rotate-baseline-secret` or `--child-secret`, and a newly generated one is only written to `appsettings.json`, never printed.
- `--child-secret` stores the secret in the Baseline project's `appsettings.json`, so don't commit that file to a repository that Child projects are created from: set the secret as the environment variable `Umbraco__Deploy__Settings__ChildApiSecrets__<name>` on the Baseline project's Live environment instead (`deploy init` warns about this).
- To rotate a child secret without downtime, add `<name>-1` with `--child-secret`, set it on the Child project, then remove `<name>` with `--remove-child-secret`.
- A Baseline secret without a `BaselineUrl` only gets a warning: it is only used for calls to the Baseline project.
- Baseline connections need Umbraco.Deploy.OnPrem 17.4.0 or later (17.x) or 18.2.0 or later (18.x); with an older version the settings are ignored, which `deploy init` warns about.
- `deploy schema deploy` is unaffected: it is still signed with the project's ApiSecret.

## Umbraco Deploy schema deployments

`umbraco deploy schema deploy` makes an Environment update its schema from the deployed Umbraco Deploy schema files and waits until it completes or fails, as `TriggerDeploy.ps1` does in Umbraco Deploy's CI/CD pipelines. Run it after deploying the Umbraco project, with the Environment's name and the `appsettings.json` or `umbraco-cloud.json` to read the Environment's URL and the ApiSecret from, or the folder containing them (an Umbraco project or its published output):

```sh
umbraco deploy schema deploy --config publish --environment Live
```

Or give the URL directly, with the ApiSecret in the `UMBRACO_DEPLOY_API_SECRET` environment variable (secrets are never options):

```sh
UMBRACO_DEPLOY_API_SECRET="$DEPLOY_API_SECRET" umbraco deploy schema deploy --url https://www.example.com --reason "Release 42"
umbraco deploy schema show --url https://www.example.com --wait   # the current schema deployment
```

| Option | Environment variable | Meaning |
|---|---|---|
| `--environment`, `-e` | | The Environment's name in the Umbraco Deploy configuration (`Workspaces`) in `--config` |
| `--url` | | The Environment's URL, instead of `--environment` |
| `--config` | | The `appsettings.json` or `umbraco-cloud.json`, or the folder containing them (default: the current directory) |
| | `UMBRACO_DEPLOY_API_SECRET` | The ApiSecret (default: the one in `--config`) |
| | `UMBRACO_DEPLOY_API_KEY` | The obsolete ApiKey, instead of an ApiSecret |

- The requests are signed with the ApiSecret, so it must be the one the Environment uses, and the clocks of both machines must agree within 20 minutes.
- Umbraco Deploy only keeps the status in memory: when it stays unknown for a minute (the site restarted, or another Umbraco Deploy operation was running), the schema deployment is started once more.
- Other options: `--reason` (logged by Umbraco Deploy), `--no-wait`, `--timeout` (default 15m); `show [<task-id>]` with `--wait`.

## Output and exit codes

Results are written to stdout (`-o text`, the default, or `-o json`); progress and diagnostics go to stderr. Live progress is shown in interactive terminals and replaced by plain lines when stderr is redirected, in CI, or with `--no-progress`. `--verbose` adds HTTP requests and error details.

| Exit code | Meaning |
|---|---|
| 0 | Success |
| 1 | Error |
| 2 | Invalid usage |
| 3 | The deployment or schema deployment failed |
| 4 | Timed out waiting (the deployment or schema deployment continues) |
| 5 | Umbraco Cloud or Umbraco Deploy rejected the credentials |
| 130 | Cancelled (a started deployment or schema deployment continues) |

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to report issues and contribute.

```sh
dotnet build
dotnet test --filter "TestCategory!=Integration&TestCategory!=Smoke"   # unit tests
dotnet test --filter "TestCategory=Integration"                        # runs real git and dotnet
dotnet run --project src/Umbraco.Cli -- --help
```

Commands for a product or service go in a top-level command group named after it (such as `cloud` or `deploy`), with the same name as a tag in `PackageTags` (`Directory.Build.props`); a test checks this. The Umbraco Cloud smoke tests (`TestCategory=Smoke`) only run when `UMBRACO_CLOUD_PROJECT_ID` and `UMBRACO_CLOUD_API_KEY` are set. See [GLOSSARY.md](GLOSSARY.md) for the domain language and [docs/adr](docs/adr) for architecture decisions.

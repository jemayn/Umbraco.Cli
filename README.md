# Umbraco CLI

A cross-platform command-line tool for Umbraco products and services in development and CI pipelines. Commands are grouped by product or service:

```
umbraco cloud init
umbraco cloud deployments upload|deploy|show
umbraco deploy init
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
- `umbraco-cloud.json` gets placeholder Umbraco ID values. They're only used for local login (Umbraco Cloud environments get their own values through environment variables), so locally the backoffice login works, but Umbraco ID login needs the real local-login values from a cloned Cloud project's `umbraco-cloud.json`. Use `--identity-client-id`/`--identity-client-secret` (or `UMBRACO_CLOUD_IDENTITY_CLIENT_ID`/`UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET`) to provide real values.
- It restores and builds the project afterwards (`--no-build` to skip the build).
- Then run `umbraco deploy init` (the ApiSecret for Umbraco Deploy) and run the project locally once before publishing it, so Umbraco installs and Umbraco Deploy writes the schema (`umbraco/Deploy/Revision`) that is deployed.

## Umbraco Cloud deployments

These commands use the [Umbraco Cloud CI/CD API](https://docs.umbraco.com/umbraco-cloud/build-and-customize-your-solution/handle-deployments-and-environments/umbraco-cicd/umbraco-cloud-api). Get the project ID and CI/CD API key from the Umbraco Cloud Portal (**Configuration > CI/CD Flow**) and pass them as options or environment variables (they are never stored):

| Option | Environment variable |
|---|---|
| `--project-id` | `UMBRACO_CLOUD_PROJECT_ID` |
| `--api-key` | `UMBRACO_CLOUD_API_KEY` |

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

`umbraco deploy init` ensures an ApiSecret is configured and adds the environments of a site. Run it in the site's project directory (or use `--project`):

```sh
umbraco deploy init \
  --environment "Name=Local;Type=development;Url=https://localhost:44307" \
  --environment "Name=Live;Type=live;Url=https://www.example.com" \
  --current-environment Local
```

- **Umbraco Deploy On-Premises** (`Umbraco.Deploy.OnPrem`): writes `Umbraco:Deploy:Settings:ApiSecret` and `Umbraco:Deploy:Project` in `appsettings.json`.
- **Umbraco Deploy Cloud** (`Umbraco.Deploy.Cloud`): writes `Deploy:Settings:ApiSecret` and `Deploy:Project:Workspaces` in `umbraco-cloud.json`. No ApiSecret is written while an ApiKey is configured, because Umbraco Cloud git-based projects still use it. Use `--project-id` to set the project ID when it isn't configured yet.

The package is detected with `dotnet package list`. A valid existing ApiSecret is kept (use `--rotate-secret` to replace it) and a newly generated one is printed: every environment must be configured with the same value. `--environment` replaces the configured list, keeping the IDs of environments with the same name. Comments and formatting of the settings file are preserved. Use `--dry-run` to see the changes without writing them.

## Output and exit codes

Results are written to stdout (`-o text`, the default, or `-o json`); progress and diagnostics go to stderr. Live progress is shown in interactive terminals and replaced by plain lines when stderr is redirected, in CI, or with `--no-progress`. `--verbose` adds HTTP requests and error details.

| Exit code | Meaning |
|---|---|
| 0 | Success |
| 1 | Error |
| 2 | Invalid usage |
| 3 | The deployment failed |
| 4 | Timed out waiting (the deployment continues) |
| 5 | Umbraco Cloud rejected the credentials |
| 130 | Cancelled (a started deployment continues) |

## Development

```sh
dotnet build
dotnet test --filter "TestCategory!=Integration&TestCategory!=Smoke"   # unit tests
dotnet test --filter "TestCategory=Integration"                        # runs real git and dotnet
dotnet run --project src/Umbraco.Cli -- --help
```

Commands for a product or service go in a top-level command group named after it (such as `cloud` or `deploy`), with the same name as a tag in `PackageTags` (`Directory.Build.props`); a test checks this. The Umbraco Cloud smoke tests (`TestCategory=Smoke`) only run when `UMBRACO_CLOUD_PROJECT_ID` and `UMBRACO_CLOUD_API_KEY` are set. See [GLOSSARY.md](GLOSSARY.md) for the domain language, [docs/adr](docs/adr) for architecture decisions and [docs/superpowers/specs](docs/superpowers/specs) for the design.

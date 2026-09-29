# Umbraco CLI

A cross-platform command-line tool for Umbraco products and services in development and CI pipelines. Commands are grouped by product or service:

```
umbraco cloud deployments upload|deploy|show
umbraco deploy init
```

## Installation

Requires the [.NET SDK 10](https://dotnet.microsoft.com/download) or later.

```sh
dotnet tool install --global Umbraco.Cli
umbraco --help
```

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

# Contributing to the Umbraco CLI

Thanks for wanting to contribute to the Umbraco CLI! This guide explains how to report issues, build the CLI, and get a pull request merged.

By contributing you agree to the [Code of Conduct](https://github.com/umbraco/.github/blob/main/.github/CODE_OF_CONDUCT.md), and that your contributions are licensed under the [MIT license](LICENSE).

## Reporting issues

- **Bugs and feature requests**: open an [issue](https://github.com/umbraco/Umbraco.Cli/issues/new/choose) with one of the issue forms. Remove any secrets (API keys, ApiSecrets, client secrets, connection strings) from the commands and output you paste.
- **Questions**: ask on the [Umbraco forum](https://forum.umbraco.com); the issue tracker is not for support questions.
- **Documentation for Umbraco products**: report it in [UmbracoDocs](https://github.com/umbraco/UmbracoDocs/issues).
- **Security issues**: never report them as GitHub issues. Report them to security@umbraco.com, as described in [how to report a vulnerability in Umbraco](https://umbraco.com/about-us/trust-center/security-and-umbraco/how-to-report-a-vulnerability-in-umbraco/).

## Before you start

For anything bigger than a small fix, open an issue (or comment on an existing one) first, so we can agree on the approach before you spend time on it. Issues labelled `community/up-for-grabs` are a good place to start.

## Building and testing

You need the .NET SDK version in [global.json](global.json) (or a later feature band) and git.

```sh
git clone https://github.com/umbraco/Umbraco.Cli.git
cd Umbraco.Cli
dotnet build
dotnet test --filter "TestCategory!=Integration&TestCategory!=Smoke"   # unit tests
dotnet test --filter "TestCategory=Integration"                        # runs real git and dotnet, and queries nuget.org
dotnet run --project src/Umbraco.Cli -- --help
```

The Umbraco Cloud smoke tests (`TestCategory=Smoke`) only run when `UMBRACO_CLOUD_PROJECT_ID` and `UMBRACO_CLOUD_API_KEY` are set; you don't need them to contribute.

To try the CLI as a global tool, pack it and install it from the local folder:

```sh
dotnet pack src/Umbraco.Cli --output artifacts
dotnet tool update --global Umbraco.Cli --add-source artifacts --prerelease
```

## Making changes

- Branch from `develop` and target `develop` with your pull request. Name the branch after the change, such as `feature/deploy-schema-timeout` or `bugfix/cloud-init-cpm`.
- Keep pull requests focused on one change, and fill in the pull request template, including how to test it.
- Add or update tests for the change. Tests use NUnit and Moq, and fakes in `tests/Umbraco.Cli.Tests/TestHelpers` instead of real processes, the network or the system clock.
- Update the [README](README.md) when you add or change a command or option.

### Code style

- The code style is defined in [.editorconfig](.editorconfig) and enforced by the build: the CI build uses `-warnaserror`, so run `dotnet build -warnaserror` before pushing.
- Follow the existing code: match the naming, comment density and idioms of the surrounding code.
- The CLI is trim and Native AOT compatible: use the source-generated `System.Text.Json` contexts (such as `CliJsonContext`) instead of reflection-based serialization, and don't add dependencies that aren't trim-safe.
- Don't add or upgrade package dependencies as part of an unrelated change.

### Commands

- Commands for a product or service go in a top-level command group named after it (such as `cloud` or `deploy`), with the same name as a tag in `PackageTags` in [Directory.Build.props](Directory.Build.props); a test checks this.
- Results go to stdout (as text, or JSON with `-o json`), and progress, warnings and errors go to stderr. Use the exit codes in `ExitCodes`.
- Commands must work non-interactively in CI/CD pipelines: never require a prompt when input is redirected or `--non-interactive` is given.

### Secrets

- Never generate a secret for Umbraco Cloud or write one to a file that is committed: Umbraco Cloud provisions its secrets ([ADR 0008](docs/adr/0008-umbraco-cloud-secrets-are-provisioned-by-umbraco-cloud.md)).
- Never write a secret to the output (text, JSON or `--verbose`), so it can't end up in CI/CD logs.
- Only read secrets from environment variables, never from command-line options ([ADR 0009](docs/adr/0009-secrets-are-only-read-from-environment-variables.md)), and when one is missing, fail with an error that names the variable and says how to get the value.

### Domain language and decisions

- Use the terms in [GLOSSARY.md](GLOSSARY.md) for domain concepts, in code, tests, output and documentation.
- Significant decisions are recorded as architecture decision records in [docs/adr](docs/adr). Add one when your change makes a decision that others would otherwise question, and mark the ADRs it supersedes.

## Releases

Releases are made by the maintainers. Versions are calculated by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) from [version.json](version.json): releases are prepared on a `release/<version>` branch and tagged `release-<version>`.

The build and release pipeline runs in Umbraco's Azure DevOps ([devops/azure-pipelines.yml](devops/azure-pipelines.yml)):

- Pull requests are built and tested on Linux and Windows.
- Every build of `develop`, `release/*` and `main` (and their `v*/` variants) is pushed to the Umbraco pre-releases feed on MyGet.
- Publishing a GitHub release creates its `release-<version>` tag. The pipeline checks that the tag matches the version, and after an approval pushes the package to NuGet and uploads the SBOM to Dependency-Track.

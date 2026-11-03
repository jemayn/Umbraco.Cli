# Local development values are pulled from Umbraco Cloud into a repository-local file

**Provisional:** the Umbraco Cloud endpoint isn't available yet, and how the values are stored hasn't been settled. The endpoint path (`GET /v2/projects/{projectId}/local-development/values`), its response (`{ "values": { "NAME": "value" } }`) and the storage below are a best guess, isolated in `CloudApiClient.GetLocalDevelopmentValuesAsync`, `LocalDevelopmentValuesResponse` and `LocalEnvironmentFile`, to be aligned when the endpoint is designed.

`umbraco cloud secrets pull` fetches the configuration values and secrets a Cloud project needs for local development from Umbraco Cloud, authenticated with the project's CI/CD API key, and writes them as environment variables to `.env.umbraco` at the root of the git repository. Configuration keys are written as environment variable names (`Umbraco:CMS:...` becomes `Umbraco__CMS__...`, as .NET configuration reads environment variables). The file is added to the repository's `.gitignore` before it's written, unless git already ignores it, and deployments leave it out (it matches `.env.*`). When the CLI runs in the repository, it reads the environment variables it needs from the file too, with variables set in the process environment taking precedence, so the same commands work locally and in CI/CD pipelines.

`umbraco cloud init` pulls the values when `UMBRACO_CLOUD_PROJECT_ID` and `UMBRACO_CLOUD_API_KEY` are set, and otherwise explains how to set them. A command that needs a value that can be pulled, and doesn't find it, says to run `umbraco cloud secrets pull`. The project ID and CI/CD API key themselves can't be pulled: they come from the Umbraco Cloud Portal.

## Considered Options

- **.NET user secrets**: scoped to the project, outside the repository and loaded by ASP.NET Core in Development, but the CLI's own commands (such as `umbraco deploy schema`) wouldn't see them as environment variables, and they need a project file rather than a repository.
- **`appsettings.Local.json`** (gitignored): read by the site with an extra configuration source, but not by the CLI, and a settings file is easy to commit by mistake in repositories that don't ignore it.
- **Print the values for the user to export**: nothing stored, but the values end up in terminal scrollback and logs, which ADR 0008 rules out.

## Consequences

- The values are on disk in plain text, like user secrets; the file must never be committed, which the CLI enforces only through `.gitignore`.
- The Umbraco site doesn't read `.env.umbraco` by itself. How a locally running site gets the values (a dotenv configuration provider, `launchSettings.json`, or user secrets as well) is still open.
- Until the endpoint exists, `umbraco cloud secrets pull` fails with a 404, and `umbraco cloud init` only warns.

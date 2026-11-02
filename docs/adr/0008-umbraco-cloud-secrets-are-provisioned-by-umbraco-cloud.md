# Umbraco Cloud secrets are provisioned by Umbraco Cloud, never by the CLI

The CLI never generates a secret for Umbraco Cloud, and never writes one to a file that is tracked by git or sent to Umbraco Cloud. Secrets for Umbraco Cloud projects (the Umbraco Deploy ApiSecret and ApiKey, the Baseline settings, and the Umbraco ID client secret) are created and held by Umbraco Cloud, and are pulled down for local development rather than pushed up from a developer's machine. `umbraco deploy init` therefore only writes the Environments (and the project ID) to `umbraco-cloud.json` for Umbraco Deploy Cloud, `--rotate-secret` is a usage error there, and `umbraco cloud init` only writes placeholder Umbraco ID values to a new `umbraco-cloud.json`.

This supersedes [ADR 0002](0002-cloud-apisecret-only-without-apikey.md) and [ADR 0003](0003-never-embed-the-identity-secret.md), and applies the reasoning of [ADR 0007](0007-apisecret-per-project-and-baseline-secret-per-connection.md) (Umbraco Cloud provisions the Baseline settings) to every Umbraco Cloud secret.

## Considered Options

- **Generate the ApiSecret locally and deploy it with `--skip-preserve-umbraco-cloud-json`** (ADR 0002): works without Umbraco Cloud support, but puts a secret in a file that is committed, and makes a developer's machine the source of a secret that Umbraco Cloud should own.
- **Accept the real Umbraco ID values as options or environment variables and write them to `umbraco-cloud.json`** (ADR 0003): gives working local Umbraco ID login, but writes a secret to a committed file.

## Consequences

- With placeholder Umbraco ID values, the backoffice login works locally but Umbraco ID login doesn't, until the values can be pulled from Umbraco Cloud.
- When Umbraco Cloud provides an endpoint to pull configuration values and secrets for local development, the CLI fetches them from it into a location that isn't tracked by git (such as user secrets), instead of into `umbraco-cloud.json` or `appsettings.json`.
- Umbraco Deploy On-Premises is unaffected: there is no Umbraco Cloud to provision its secrets, so `umbraco deploy init` still generates them ([ADR 0001](0001-apisecret-in-appsettings-json.md), [ADR 0007](0007-apisecret-per-project-and-baseline-secret-per-connection.md)), but never prints them, so they can't end up in CI/CD logs.

# Never embed the Umbraco ID client secret

**Superseded by [ADR 0008](0008-umbraco-cloud-secrets-are-provisioned-by-umbraco-cloud.md):** the CLI no longer accepts the real Umbraco ID values (the `--identity-client-id`/`--identity-client-secret` options and `UMBRACO_CLOUD_IDENTITY_CLIENT_ID`/`UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET` are removed) and only writes placeholders.

`umbraco cloud init` writes the `Identity` block of `umbraco-cloud.json` from values given explicitly (`--identity-client-id`/`--identity-client-secret` or `UMBRACO_CLOUD_IDENTITY_CLIENT_ID`/`UMBRACO_CLOUD_IDENTITY_CLIENT_SECRET`), and otherwise from clearly fake, non-empty placeholders, instead of shipping the real Umbraco ID values in the CLI. The values in `umbraco-cloud.json` are only used for the local login flow (running an Umbraco project outside Umbraco Cloud): each Umbraco Cloud environment gets its own ClientId and ClientSecret through environment variables, so the file's values don't affect Umbraco ID login on Umbraco Cloud. The ClientSecret is a secret and organisation policy forbids committing credentials; placeholders are enough for a project to boot with the regular backoffice login, because Umbraco.Cloud.Cms only requires the values to be non-empty.

## Considered Options

- **Embed the real values**: gives working local Umbraco ID login out of the box, but publishes a live client secret in a public repository and NuGet package, and it would have to be rotated in every CLI release when Umbraco Cloud rotates it.
- **Fetch the values for a `--project-id`**: the right long-term approach, but no external API returns them today; it needs a user login (`umbraco cloud login`) and a new endpoint.
- **Clone the Cloud project's repository to read `umbraco-cloud.json`**: works today, but needs git and the Cloud environment's clone URL, which a project ID can't be resolved to.

## Consequences

With placeholders, Umbraco ID login doesn't work locally (the regular backoffice login does) until the real local-login values are given; on Umbraco Cloud it's unaffected, also when the placeholders are deployed with published output.

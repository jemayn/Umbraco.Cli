# The ApiSecret is per project and each Baseline connection has its own Baseline secret

Umbraco Deploy no longer needs one ApiSecret shared by related projects: each project has its own ApiSecret, shared only by its own Environments, and a Child project restores from its Baseline project with a separate Baseline secret for each Baseline connection. `umbraco deploy init` therefore writes `BaselineUrl` and `BaselineApiSecret` (Child project) and `ChildApiSecrets` entries (Baseline project) for Umbraco Deploy On-Premises only, and never generates a Baseline secret or child secret unless asked to with `--rotate-baseline-secret` or `--child-secret`. For Umbraco Deploy Cloud it only reports the settings, because Umbraco Cloud provisions them.

This revises the assumption in [ADR 0001](0001-apisecret-in-appsettings-json.md) that one ApiSecret is shared by every Environment (and so by every project that exchanges content), and extends [ADR 0002](0002-cloud-apisecret-only-without-apikey.md) (no Cloud writes that Umbraco Cloud would replace or that its git-based projects don't read) to the Baseline settings: using a Baseline option with Umbraco Deploy Cloud is a usage error.

## Considered Options

- **Reuse the ApiSecret for the Baseline connection**: nothing new to configure, but a project would have to share its ApiSecret with every project it is related to.
- **Write the Baseline settings on Cloud too**: consistent with On-Premises, but Cloud provisions these values and would replace what the CLI wrote.
- **Generate the Baseline secret with `--baseline-url`**: fewer options, but a secret would appear without being asked for, and the Baseline project must be given the same value before it works.

## Consequences

- A generated Baseline secret or child secret is written to `appsettings.json` and never printed (see [ADR 0008](0008-umbraco-cloud-secrets-are-provisioned-by-umbraco-cloud.md)), and the same value must be set on the other side of the Baseline connection (as `BaselineApiSecret` on the Child project, or as a `ChildApiSecrets` entry on the Baseline project's Live environment).
- A child secret written to the Baseline project's `appsettings.json` must not be committed to a repository that Child projects are created from (Umbraco Deploy's own guidance), so the CLI warns and points to the environment variable that sets it instead.
- Rotating a child secret without downtime means adding a second entry, switching the Child project to it, then removing the first.
- Baseline connections need Umbraco.Deploy.OnPrem 17.4.0 or later (17.x) or 18.2.0 or later (18.x); with older versions the settings are ignored, so the CLI warns.
- `umbraco deploy schema` is unaffected: its requests are still signed with the project's ApiSecret.

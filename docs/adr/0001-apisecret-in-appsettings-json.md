# Write the Umbraco Deploy On-Premises ApiSecret to appsettings.json

`umbraco deploy init` writes the generated ApiSecret to `appsettings.json` rather than .NET user-secrets, because Umbraco Deploy can only restore and transfer content when every Environment (local and hosted) is configured with the same ApiSecret, and user-secrets are only loaded in Development, so the value would never reach hosted Environments.

## Considered Options

- **User-secrets for the ApiSecret, appsettings.json for Environments**: keeps the secret out of source control, but leaves hosted Environments without it and makes local and hosted secrets diverge.
- **A caller-chosen settings file**: flexible, but pushes the "which file does every Environment load?" problem onto every user.

## Consequences

The ApiSecret ends up in a file that is usually committed. The CLI prints the newly generated ApiSecret so it can be moved to environment variables or a secret store for hosted Environments when committing it is not acceptable.

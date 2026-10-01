# Schema deployments use the HMAC-authenticated Schema API

`umbraco deploy schema deploy|show` call Umbraco Deploy's Schema API (`/umbraco/umbracodeploy/extract/start/{reason}` and `/umbraco/umbracodeploy/statusreport/get/{taskId}`), authenticated with an HMAC bearer token signed with the ApiSecret (or the obsolete ApiKey), as `TriggerDeploy.ps1` and the documented CI/CD pipelines do, instead of the backoffice Management API (`/umbraco/deploy/management/api/v1/operation/deploy-schema`), because a pipeline already has the ApiSecret (it's in the deployed `appsettings.json` or `umbraco-cloud.json`, or a pipeline secret), while the Management API needs a backoffice user's access token with access to the Settings section, which a CLI would have to obtain through an interactive login or a dedicated API user.

## Considered Options

- **Management API**: reports a busy Environment (409) instead of skipping silently and returns a session ID to poll, but needs backoffice user authentication.
- **Trigger files** (`umbraco/Deploy/deploy`): need file access to the Environment and give no status beyond marker files.

## Consequences

- The token is only valid within Umbraco Deploy's session timeout (20 minutes by default), so the clocks of the CI agent and the Environment must roughly agree, and a fresh token is created for every request.
- The status is only kept in memory and (before Umbraco Deploy 17.4) a start is skipped when another operation runs, so the CLI tolerates an unknown status for a while and starts the schema deployment once more when it stays unknown (as `TriggerDeploy.ps1` does).
- The reason is part of the signed path, so it's limited to characters that are escaped the same way by the CLI and Umbraco Deploy.

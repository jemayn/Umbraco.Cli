# `umbraco deploy schema deploy|show` — design

Date: 01-10-2026
Status: approved design, implemented (see [the plan](../plans/2026-10-01-umbraco-deploy-schema.md))

## Summary

`umbraco deploy schema deploy` makes an Environment run a Schema deployment: Umbraco Deploy on that Environment updates its schema from the deployed schema files (`umbraco/Deploy/Revision`), and the CLI waits until it completes or fails, like `TriggerDeploy.ps1 -Action TriggerWithStatus` in the documented CI/CD pipelines. `umbraco deploy schema show` shows (and optionally waits for) a schema deployment. Both behave like `umbraco cloud deployments deploy|show` (polling, timeout, Ctrl+C, output and exit codes). Terminology follows [GLOSSARY.md](../../../GLOSSARY.md) (Schema deployment, Environment, ApiSecret, ApiKey); the API choice is [ADR 0006](../../adr/0006-schema-deployments-use-the-hmac-schema-api.md).

## Sources

- Umbraco Deploy v17 (`v17/dev`): `Umbraco.Deploy.Infrastructure/Controllers/Extract`, `Controllers/StatusReport`, `Authentication/BearerTokenValidator.cs`, `Core/Authentication/HMACAuthentication.cs`, `Work/WorkEnvironment.cs`, `Disk/DiskService.cs`; `Umbraco.Deploy.OnPrem/TriggerDeploy.ps1`.
- Umbraco Deploy documentation: CI/CD pipeline, Azure DevOps, full example.

## Umbraco Deploy Schema API

Available in both Umbraco Deploy On-Premises and Umbraco Deploy Cloud:

- `POST {url}/umbraco/umbracodeploy/extract/start/{reason}` ⇒ `{ "TaskId", "TaskStarted" }`. Up to 17.3 the work is queued (`TaskStarted` is always true) and silently skipped when another Umbraco Deploy operation runs. Since 17.4 a start joins the schema deployment that is already queued or running (or, with a read-only disk, the one that completed on startup): `TaskStarted` is then false and `TaskId` is that schema deployment's, which the CLI waits for.
- `GET {url}/umbraco/umbracodeploy/statusreport/get/{taskId}` and `…/statusreport/getcurrent` ⇒ `{ Id, Status, Comment, Percent (-1 = unknown), Exception { Type, Detail, … }, Log (once final), MismatchList [{ Udi, Name, Alias }], IsHeartbeat, Type }` (PascalCase, enums as strings).
- `Status`: `New`/`Executing` (running), `Completed`, `Failed`/`Cancelled`/`TimedOut`/`Mismatch` (failed), `Unknown` (not found). The status is kept in memory only; an unknown task ID can also make the request fail (500).
- Authentication: `Authorization: Bearer base64("{signature}:{nonce}:{timestamp}")`, where `signature = base64(HMACSHA256(key, path and query + Unix seconds + nonce))`, the path and query being exactly what's sent (including a path base; `//` collapsed). The key is the Base64-decoded ApiSecret, or the UTF-8 bytes of the ApiKey. Tokens are accepted within the session timeout (20 minutes). Failures: 401 (problem details), 403 when the site requires HTTPS.

## Commands

```
umbraco deploy schema deploy (--environment <name> | --url <url>) [--config <file|dir>] [--api-secret <base64> | --api-key <key>]
                             [--reason <text>] [--no-wait] [--timeout <duration>]
umbraco deploy schema show [<task-id>] (--environment <name> | --url <url>) [--config <file|dir>] [--api-secret <base64> | --api-key <key>]
                           [--wait] [--timeout <duration>]
```

| Option | Meaning |
|---|---|
| `--environment`, `-e` | The Environment name (case-insensitive) in `Deploy:Project:Workspaces` of `umbraco-cloud.json`, then `Umbraco:Deploy:Project:Workspaces` of `appsettings.json`; its `Url` is used. Unknown ⇒ exit 2, listing the configured names |
| `--url` | The Environment's URL instead (absolute HTTPS, HTTP only for localhost; may include a path base). Exactly one of `--environment`/`--url` |
| `--config` | The `appsettings.json` or `umbraco-cloud.json`, or the folder containing them (an Umbraco project or its published output; default: the current directory). A file other than `umbraco-cloud.json` is read as app settings (such as `appsettings.Production.json`). Missing files are fine when options or environment variables give everything; the same option name is used by `cloud deployments`, where `--path` is the folder to upload |
| `--api-secret`, `--api-key` | The credentials (mutually exclusive); also `UMBRACO_DEPLOY_API_SECRET`/`UMBRACO_DEPLOY_API_KEY` |
| `--reason` | Logged by Umbraco Deploy; default `Umbraco CLI`; letters, digits, spaces and `-_.~` only, because it's part of the signed path |
| `--no-wait` / `--wait` | Return after the start / wait for `show` |
| `--timeout` | Default 15m (`Durations`) |

The credentials come from the first source with an ApiKey or ApiSecret: the options, the environment variables, `umbraco-cloud.json` (`Deploy:Settings`), `appsettings.json` (`Umbraco:Deploy:Settings`). Within a source the ApiKey wins, as in Umbraco Deploy. An invalid Base64 ApiSecret or no credentials ⇒ exit 2. Credentials are never stored or printed (`DeployCredentials.ToString()` is masked; verbose output names the source only).

## Behaviour

- Starts are never retried on errors (a retry could start a second schema deployment); a start without a response within 100 seconds ⇒ exit 1 ("it may have started anyway").
- Waiting polls every 3 seconds (the `TriggerDeploy.ps1` default; Umbraco Deploy has no rate limit), reporting state changes and the progress comment with its percentage.
- An `Unknown` status and transient failures (no response, 429, 5xx) are tolerated for 60 seconds since the start or the last known status. Beyond that, `deploy` starts the schema deployment once more and continues with the new task ID (`restarted` in the result); otherwise (again, or for `show --wait`) ⇒ exit 1 with a hint that the site may have restarted or was busy.
- `Completed` ⇒ exit 0; `Failed`, `Cancelled`, `TimedOut` or `Mismatch` ⇒ `error: Schema deployment <id> to <environment> failed: <exception detail, mismatches or status>.` and exit 3; the Umbraco Deploy log is printed with `--verbose`.
- Timeout ⇒ exit 4 and Ctrl+C ⇒ exit 130, both with the hint `umbraco deploy schema show <id> --wait`.
- 401 ⇒ exit 5 (hint: the ApiSecret must match the Environment's and the clocks must agree within 20 minutes); 403 ⇒ HTTPS hint; 404 ⇒ "the Schema API wasn't found"; unreachable ⇒ `Could not reach <url>: …`; a non-JSON response ⇒ "unexpected response".
- `show` without a task ID shows the current (or last) task; an unknown task ⇒ exit 1.

## Output

Text: `Schema deployment <id> to <environment>: Completed.` (`… started.` with `--no-wait`). JSON (public contract):

```json
{
  "taskId": "…",
  "environment": "Live",
  "url": "https://www.example.com",
  "status": "Completed",
  "comment": "Completed",
  "percent": 100,
  "log": "…",
  "restarted": false
}
```

`exception` (`{ type, detail }`) and `mismatches` (UDIs) are added when set.

## Testing

- **Unit**: the bearer token against vectors computed with `TriggerDeploy.ps1`'s algorithm; target and credential resolution (precedence, Workspaces lookup, invalid values); the API client (routes, escaping, token validity, error mapping, timeouts); the waiter (progress, Unknown and transient failures, restart once, timeout, cancellation); the commands end to end with a fake HTTP handler.
- **Manual**: against a real Environment, as the request is outward-facing.

## Out of scope

- The Management API, trigger files and other Umbraco Deploy operations (clean, export, signatures).
- Layering `appsettings.{environment}.json`.

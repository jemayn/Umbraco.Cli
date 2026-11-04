# CI/CD pipeline examples

> [!NOTE]
> These examples are a starting point and haven't been validated in a real pipeline yet. They install the CLI from NuGet with `--prerelease`, which works once a beta is published.

Both examples use one secret, `UMBRACO_CLOUD_API_KEY`. The CLI reads it from the environment, so it's never passed as an argument or written to the logs. The project ID is read from `UMBRACO_CLOUD_PROJECT_ID` or, in a checked-out repository, from the `umbraco-cloud.json` of the Umbraco project.

## GitHub Actions

| Example | Deployment type | Flow |
| --- | --- | --- |
| [`git-deployment.yml`](github-actions/git-deployment.yml) | Git (source code; Umbraco Cloud builds it) | The flow of the documented [`main-more-targets.yml` sample](https://github.com/umbraco/Umbraco.Cloud.CICDFlow.Samples/blob/main/V2/powershell/github/advanced/main-more-targets.yml): sync changes from Umbraco Cloud, then deploy `main` to the left-most environment and `flexible` to a flexible environment. |
| [`artifact-deployment.yml`](github-actions/artifact-deployment.yml) | Artifact (published output) | `dotnet publish` and upload once, then deploy the same artifact to Development and Live, passing the artifact ID between jobs. |

`git-deployment.yml` still uses the sample's `cloud-sync.yml` workflow and its PowerShell scripts to get the changes made on Umbraco Cloud since the latest deployment, because the CLI has no command for that yet. Copy them from the samples repository (`V2/powershell/github/cloud-sync.yml` to `.github/workflows`, and `Get-LatestDeployment.ps1`, `Get-ChangesById.ps1` and `Apply-Patch.ps1` from `V2/powershell` to `.github/powershell`).

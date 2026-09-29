# Only write an ApiSecret to umbraco-cloud.json when no ApiKey is set

For Umbraco Deploy Cloud, `umbraco deploy init` writes a generated ApiSecret to `Deploy.Settings.ApiSecret` in `umbraco-cloud.json` only when `Deploy.Settings.ApiKey` is not set, and never removes or changes an existing ApiKey. Umbraco Cloud git-based projects still read the ApiKey from the Cloud repository's copy of `umbraco-cloud.json`, and that copy replaces the deployed file unless `skipPreserveUmbracoCloudJson` is set, so replacing the ApiKey locally would either be discarded or break the Environments that still use it.

## Consequences

Git-based Cloud projects keep using the ApiKey; `init` still writes their Environments and says why no ApiSecret was written. Unlike Umbraco Deploy On-Premises, there is no `--rotate-secret` migration from ApiKey to ApiSecret on Cloud.

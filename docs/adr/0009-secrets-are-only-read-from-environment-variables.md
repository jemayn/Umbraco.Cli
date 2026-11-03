# Secrets are only read from environment variables

The CLI never accepts a secret as a command-line option: the Umbraco Cloud CI/CD API key (`UMBRACO_CLOUD_API_KEY`), the Umbraco Deploy ApiSecret or ApiKey (`UMBRACO_DEPLOY_API_SECRET`, `UMBRACO_DEPLOY_API_KEY`) and the Baseline secret (`UMBRACO_DEPLOY_BASELINE_SECRET`) are only read from environment variables (or, for the ApiSecret of a schema deployment, from the Umbraco project's configuration files). Values that aren't secrets, such as the project ID, can still be options.

A value given as an option ends up in shell history, in the process list (`ps`, `/proc/<pid>/cmdline`) where other users of a build agent can read it, and in CI/CD logs that echo commands; an environment variable from a pipeline's secret store is masked in its logs.

## Considered Options

- **Keep the options, but recommend environment variables**: no breaking change, but the unsafe way keeps working and keeps appearing in examples and scripts. The CLI isn't released yet, so removing the options breaks no one.
- **Read secrets from stdin** (`--api-key-stdin`): safe, but awkward in pipelines and for several secrets; environment variables are what CI/CD systems support natively.

## Consequences

- When a required secret isn't set, the CLI fails with a usage error that names the environment variable and says where to get the value (see [ADR 0010](0010-local-development-values-are-pulled-from-umbraco-cloud.md) for values that can be pulled from Umbraco Cloud).
- Tests and documentation set environment variables instead of options.

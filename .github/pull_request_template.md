### Prerequisites

- [ ] I have read [CONTRIBUTING.md](https://github.com/umbraco/Umbraco.Cli/blob/develop/CONTRIBUTING.md), and this PR targets `develop`
- [ ] I have added steps to test this contribution in the description below

If there's an existing issue for this PR then this fixes <!-- link to the issue here! -->

### Description

<!--
    What did you change, why, and how can we test it? Include the commands you ran and their output (with any secrets removed).
-->

### Checks

- [ ] `dotnet build -warnaserror` and `dotnet test` pass
- [ ] Tests are added or updated for the change
- [ ] The README is updated for new or changed commands and options
- [ ] An ADR is added in `docs/adr` for a significant decision (only if needed)
- [ ] No secrets are generated for Umbraco Cloud, written to files that are committed, or written to the output ([ADR 0008](https://github.com/umbraco/Umbraco.Cli/blob/develop/docs/adr/0008-umbraco-cloud-secrets-are-provisioned-by-umbraco-cloud.md))

<!-- Thanks for contributing to the Umbraco CLI! -->

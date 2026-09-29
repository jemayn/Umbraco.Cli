# Umbraco CLI

A cross-platform command-line tool for Umbraco products and services in development and CI pipelines.

## Language

### Umbraco Cloud

**Project**:
An Umbraco Cloud project, identified by a project ID and accessed with a project-scoped CI/CD API key.
_Avoid_: Site, subscription

**Environment**:
A running instance of a site that content and code move between (e.g. Development, Staging, Live, or a local site), identified by an alias on Umbraco Cloud.
_Avoid_: Workspace (except when naming the Umbraco Deploy configuration key `Workspaces`), slot, stage

**Artifact**:
A zip archive uploaded to a Project, from which a Deployment is made.
_Avoid_: Package, bundle, build

**Git deployment**:
A Deployment whose Artifact contains source code that Umbraco Cloud commits to the Project's git repository and builds.

**Artifact deployment**:
A Deployment whose Artifact contains already-published output that Umbraco Cloud runs as-is.
_Avoid_: Binary deployment, publish deployment

**Deployment**:
The act of applying one Artifact to one target Environment on Umbraco Cloud, tracked until it completes or fails.
_Avoid_: Release, rollout, "deploy" as a noun

### Umbraco Deploy

**Umbraco Deploy**:
The Umbraco product that transfers and restores content and schema between Environments; always named in full to avoid confusion with a Deployment.
_Avoid_: Deploy (bare), content deployment

**Umbraco Deploy On-Premises**:
The Umbraco Deploy package for self-hosted sites, configured entirely through the site's app settings.
_Avoid_: OnPrem (except in the package name `Umbraco.Deploy.OnPrem`), self-hosted Deploy

**Umbraco Deploy Cloud**:
The Umbraco Deploy package for Umbraco Cloud sites, configured through `umbraco-cloud.json` and settings provided by Umbraco Cloud.
_Avoid_: Cloud Deploy

**Current environment**:
The Environment a site considers itself to be; configured explicitly for Umbraco Deploy On-Premises, determined by Umbraco Cloud at runtime for Umbraco Deploy Cloud.
_Avoid_: Current workspace, local environment

**ApiSecret**:
A cryptographically random value of at least 64 bytes, shared by all Environments of a site, that Umbraco Deploy uses to authenticate requests between them.
_Avoid_: Secret key, token

**ApiKey**:
The obsolete, shorter shared value that preceded the ApiSecret; still required by Umbraco Cloud git-based projects.
_Avoid_: API key (when meaning the Cloud CI/CD API key)

**CI/CD API key**:
The Umbraco Cloud credential, scoped to one Project, used to upload Artifacts and start Deployments.
_Avoid_: ApiKey

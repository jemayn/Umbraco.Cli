# Umbraco CLI

A cross-platform command-line tool for Umbraco products and services in development and CI pipelines.

## Language

### Umbraco projects and Products

**Umbraco project**:
The .NET project of an Umbraco website (its `.csproj` and code), as created by the `umbraco` template of Umbraco.Templates; the `--project` option of a command refers to it, following the .NET CLI.
_Avoid_: Site, website project, solution, Project (bare)

**Product**:
An installable Umbraco add-on (such as Forms, Deploy, Commerce, Engage, Workflow, UI Builder, Automate or AI), made up of one or more NuGet packages whose major version follows the Umbraco CMS major.
_Avoid_: Add-on, extension, plugin, package (when meaning the whole Product)

**Release type**:
Whether an Umbraco major is a long-term (LTS) or short-term (STS) supported release; 17 and every fourth major after it are LTS.
_Avoid_: Channel, track

**Lifecycle phase**:
Where an Umbraco major is in its support life — active, support, security (fixes only) or end-of-life — measured from the release of its first stable version.
_Avoid_: Support status, maintenance mode

**Integration package**:
A package that makes two Products work together (such as `Umbraco.Forms.Deploy` or `Umbraco.Engage.Forms`), added automatically when both Products are installed.
_Avoid_: Bridge package, bridging package, connector

### Umbraco Cloud

**Cloud project**:
An Umbraco Cloud project, identified by a project ID and accessed with a project-scoped CI/CD API key.
_Avoid_: Project (bare), site, subscription

**Cloud scaffold**:
The repository layout and files Umbraco Cloud creates for a new Cloud project (`.umbraco`, `src/UmbracoProject`, central package management, Cloud-specific settings), which `umbraco cloud init` reproduces for a new Umbraco project.
_Avoid_: Template (that is the Umbraco.Templates `umbraco` template), starter, boilerplate

**Environment**:
A running instance of an Umbraco project that content and code move between (e.g. Development, Staging, Live, or a local instance), identified by an alias on Umbraco Cloud.
_Avoid_: Workspace (except when naming the Umbraco Deploy configuration key `Workspaces`), slot, stage

**Artifact**:
A zip archive uploaded to a Cloud project, from which a Deployment is made.
_Avoid_: Package, bundle, build

**Git deployment**:
A Deployment whose Artifact contains source code that Umbraco Cloud commits to the Cloud project's git repository and builds.

**Artifact deployment**:
A Deployment whose Artifact contains already-published output that Umbraco Cloud runs as-is.
_Avoid_: Binary deployment, publish deployment

**Deployment**:
The act of applying one Artifact to one target Environment on Umbraco Cloud, tracked until it completes or fails.
_Avoid_: Release, rollout, "deploy" as a noun

### Umbraco Deploy

**Umbraco Deploy**:
The Umbraco Product that transfers and restores content and schema between Environments; always named in full to avoid confusion with a Deployment.
_Avoid_: Deploy (bare), content deployment

**Umbraco Deploy On-Premises**:
The Umbraco Deploy package for self-hosted Umbraco projects, configured entirely through the project's app settings.
_Avoid_: OnPrem (except in the package name `Umbraco.Deploy.OnPrem`), self-hosted Deploy

**Umbraco Deploy Cloud**:
The Umbraco Deploy package for Umbraco projects hosted on Umbraco Cloud, configured through `umbraco-cloud.json` and settings provided by Umbraco Cloud.
_Avoid_: Cloud Deploy

**Schema deployment**:
An Environment updating its schema from the Umbraco Deploy schema files that were deployed to it, tracked until it completes or fails.
_Avoid_: Deployment (an Umbraco Cloud Deployment), extraction (except in Umbraco Deploy's endpoint names), schema import

**Current environment**:
The Environment an Umbraco project considers itself to be; configured explicitly for Umbraco Deploy On-Premises, determined by Umbraco Cloud at runtime for Umbraco Deploy Cloud.
_Avoid_: Current workspace, local environment

**ApiSecret**:
A cryptographically random value of at least 64 bytes, shared by all Environments of an Umbraco project, that Umbraco Deploy uses to authenticate requests between them.
_Avoid_: Secret key, token

**ApiKey**:
The obsolete, shorter shared value that preceded the ApiSecret; still required by Umbraco Cloud git-based projects.
_Avoid_: API key (when meaning the Cloud CI/CD API key)

**CI/CD API key**:
The Umbraco Cloud credential, scoped to one Cloud project, used to upload Artifacts and start Deployments.
_Avoid_: ApiKey

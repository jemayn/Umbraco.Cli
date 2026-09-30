# Query NuGet with HttpClient and NuGet.Versioning

The CLI reads nuget.org's v3 service index and registration API with `HttpClient` and source-generated System.Text.Json, and uses only `NuGet.Versioning` for SemVer 2 ordering and version ranges, instead of the official `NuGet.Protocol` client SDK, because `NuGet.Protocol` brings in Newtonsoft.Json and the NuGet packaging and configuration stack and isn't trim-safe: a trimmed build reports trim warnings and crashes at runtime (`No parameterless constructor defined for 'NuGet.Protocol.MetadataFieldConverter'`), which would break the CLI's trim/AOT compatibility. `NuGet.Versioning` has no dependencies and trims without warnings.

## Consequences

Only nuget.org is queried to resolve versions; the project's own `nuget.config` feeds and mirrors are not. Installing still goes through `dotnet package add` and `dotnet restore`, which honour them, so a package missing from a private mirror is reported by restore.

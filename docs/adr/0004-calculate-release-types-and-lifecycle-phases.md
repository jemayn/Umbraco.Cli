# Calculate release types and lifecycle phases

The CLI calculates whether an Umbraco major is LTS or STS (17 and every fourth major after it are LTS) and its lifecycle phase (active, support, security, end-of-life) from the NuGet publish date of its first stable `Umbraco.Cms` version and the durations of Umbraco's support policy, because no official machine-readable source exists: the umbraco.com LTS page is HTML only, and releases.umbraco.com is undocumented and reports the wrong channel for older majors.

## Consequences

The calculation only holds while Umbraco keeps its policy (a major every six months, LTS every fourth major, 6/9/12 months for STS and 6/24/36 months for LTS) and publishes `x.0.0` to NuGet on the release day; a policy change needs a CLI release. Dates can differ by a few days from the official table when a NuGet package is published earlier or later than the announced release.

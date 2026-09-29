using NUnit.Framework;
using Umbraco.Cli.Cloud.Artifacts;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class ArtifactClassifierTests
{
    [TestCase("Umbraco.Core.dll")]
    [TestCase("./Umbraco.Core.dll")]
    [TestCase("umbraco.core.DLL")]
    public void Classify_UmbracoCoreAtRoot_IsArtifactDeployment(string path)
        => Assert.That(ArtifactClassifier.Classify(["web.config", path, "src/Site.csproj"]), Is.EqualTo(DeploymentType.Artifact));

    [TestCase("Site.csproj")]
    [TestCase("src/Site/Site.csproj")]
    [TestCase("Site.sln")]
    [TestCase("Site.slnx")]
    [TestCase("src\\Site.fsproj")]
    [TestCase("Legacy.VBPROJ")]
    public void Classify_ProjectOrSolutionAtAnyDepth_IsGitDeployment(string path)
        => Assert.That(ArtifactClassifier.Classify(["README.md", path]), Is.EqualTo(DeploymentType.Git));

    [Test]
    public void Classify_UmbracoCoreNotAtRoot_IsNotArtifactDeployment()
        => Assert.That(ArtifactClassifier.Classify(["bin/Umbraco.Core.dll"]), Is.Null);

    [Test]
    public void Classify_NeitherMarker_ReturnsNull()
        => Assert.That(ArtifactClassifier.Classify(["index.html", "css/site.css"]), Is.Null);
}

using Moq;
using NUnit.Framework;
using Umbraco.Cli.Cloud.Api;
using Umbraco.Cli.Cloud.Artifacts;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Tests.Cloud;

[TestFixture]
public class ArtifactUploaderTests
{
    private static readonly CloudCredentials Credentials = new("p", "k");

    private string _zip = null!;
    private Mock<ICloudApiClient> _api = null!;

    [SetUp]
    public void SetUp()
    {
        _zip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(_zip, new byte[100]);
        _api = new Mock<ICloudApiClient>(MockBehavior.Strict);
    }

    [TearDown]
    public void TearDown() => File.Delete(_zip);

    [Test]
    public void DefaultMaxArtifactSize_Is256MiB()
        => Assert.That(ArtifactUploader.DefaultMaxArtifactSize, Is.EqualTo(268435456));

    [Test]
    public void UploadAsync_LargerThanTheLimit_FailsBeforeUploading()
    {
        ArtifactUploader uploader = CreateUploader(maxArtifactSize: 99);

        UsageException exception = Assert.ThrowsAsync<UsageException>(() => uploader.UploadAsync(Credentials, new ArtifactSource(_zip, null, null, null, null), requireDotUmbraco: false, CancellationToken.None))!;

        Assert.That(exception.Message, Does.Contain("100 B").And.Contain("at most 99 B"));
    }

    [Test]
    public async Task UploadAsync_AtTheLimit_Uploads()
    {
        _api.Setup(x => x.UploadArtifactAsync(Credentials, _zip, "d", "v", It.IsAny<IProgress<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArtifactResponse { ArtifactId = "a-1" });

        ArtifactResponse artifact = await CreateUploader(maxArtifactSize: 100).UploadAsync(Credentials, new ArtifactSource(_zip, null, null, "d", "v"), requireDotUmbraco: false, CancellationToken.None);

        Assert.That(artifact.ArtifactId, Is.EqualTo("a-1"));
    }

    private ArtifactUploader CreateUploader(long maxArtifactSize)
        => new(_api.Object, new ArtifactZipBuilder(Mock.Of<IGit>()), new PlainReporter(TextWriter.Null, TimeProvider.System, verbose: false))
        {
            MaxArtifactSize = maxArtifactSize,
        };
}

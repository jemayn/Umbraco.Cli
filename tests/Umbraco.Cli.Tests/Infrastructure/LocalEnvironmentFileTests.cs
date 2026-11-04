using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class LocalEnvironmentFileTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void WriteAndRead_RoundTripsTheValues()
    {
        string path = Path.Combine(_directory, LocalEnvironmentFile.FileName);

        LocalEnvironmentFile.Write(path, new Dictionary<string, string> { ["B_VALUE"] = "x=y", ["A_VALUE"] = "secret+/==" });

        Assert.That(File.ReadAllText(path), Does.StartWith("# ").And.Contain("never commit"));
        Assert.That(LocalEnvironmentFile.Read(path), Is.EquivalentTo(new Dictionary<string, string> { ["A_VALUE"] = "secret+/==", ["B_VALUE"] = "x=y" }));
    }

    [Test]
    public void Read_IgnoresCommentsBlankAndInvalidLines()
    {
        string path = Path.Combine(_directory, LocalEnvironmentFile.FileName);
        File.WriteAllText(path, "# comment\n\nNOT A VALUE\n=missing-name\nNAME = value\n");

        Assert.That(LocalEnvironmentFile.Read(path), Is.EquivalentTo(new Dictionary<string, string> { ["NAME"] = " value" }));
    }

    [Test]
    public void Normalize_TurnsConfigurationKeysIntoEnvironmentVariableNames()
    {
        IReadOnlyDictionary<string, string> normalized = LocalEnvironmentFile.Normalize(new Dictionary<string, string>
        {
            ["Umbraco:Deploy:Settings:ApiSecret"] = "a",
            ["UMBRACO_DEPLOY_API_SECRET"] = "b",
        });

        Assert.That(normalized, Is.EquivalentTo(new Dictionary<string, string> { ["Umbraco__Deploy__Settings__ApiSecret"] = "a", ["UMBRACO_DEPLOY_API_SECRET"] = "b" }));
    }

    [Test]
    public void Normalize_InvalidNameOrMultilineValue_ThrowsWithoutTheValue()
    {
        Assert.That(() => LocalEnvironmentFile.Normalize(new Dictionary<string, string> { ["BAD NAME"] = "a" }), Throws.TypeOf<CliException>());

        CliException exception = Assert.Throws<CliException>(() => LocalEnvironmentFile.Normalize(new Dictionary<string, string> { ["NAME"] = "line1\nsecret-line2" }))!;
        Assert.That(exception.Message, Does.Contain("NAME").And.Not.Contain("secret-line2"));
    }

    [Test]
    public void Find_LooksUpToTheRootOfTheRepository()
    {
        string repository = Path.Combine(_directory, "repository");
        string project = Path.Combine(repository, "src", "Site");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(Path.Combine(repository, ".git"));

        Assert.That(LocalEnvironmentFile.Find(project), Is.Null);

        // A file above the repository doesn't belong to it
        File.WriteAllText(Path.Combine(_directory, LocalEnvironmentFile.FileName), "A=1");
        Assert.That(LocalEnvironmentFile.Find(project), Is.Null);

        File.WriteAllText(Path.Combine(repository, LocalEnvironmentFile.FileName), "A=2");
        Assert.That(LocalEnvironmentFile.Find(project), Is.EqualTo(Path.Combine(repository, LocalEnvironmentFile.FileName)));
    }

    [Test]
    public void LocalFileEnvironment_PrefersProcessVariablesOverTheFile()
    {
        Directory.CreateDirectory(Path.Combine(_directory, ".git"));
        File.WriteAllText(Path.Combine(_directory, LocalEnvironmentFile.FileName), "FROM_FILE=file\nBOTH=file\n");
        var inner = new FakeEnvironment(_directory) { Variables = { ["BOTH"] = "process" } };

        var environment = new LocalFileEnvironment(inner);

        Assert.That(environment.GetVariable("FROM_FILE"), Is.EqualTo("file"));
        Assert.That(environment.GetVariable("BOTH"), Is.EqualTo("process"));
        Assert.That(environment.GetVariable("MISSING"), Is.Null);
    }
}

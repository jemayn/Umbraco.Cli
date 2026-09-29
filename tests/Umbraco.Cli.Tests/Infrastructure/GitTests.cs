using Moq;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Processes;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class GitTests
{
    [Test]
    public async Task ListFilesAsync_SplitsNulSeparatedOutput()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(x => x.RunAsync("git", It.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "ls-files", "--cached", "--others", "--exclude-standard", "-z" })), "/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "a.txt\0src/b c.cs\0a.txt\0", string.Empty));

        IReadOnlyList<string> files = await new Git(runner.Object).ListFilesAsync("/repo", CancellationToken.None);

        Assert.That(files, Is.EqualTo(new[] { "a.txt", "src/b c.cs" }));
    }

    [Test]
    public void ListFilesAsync_NotAWorkTree_Throws()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(x => x.RunAsync("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(128, string.Empty, "fatal: not a git repository"));

        CliException exception = Assert.ThrowsAsync<CliException>(() => new Git(runner.Object).ListFilesAsync("/tmp", CancellationToken.None))!;

        Assert.That(exception.Message, Does.Contain("not a git repository"));
        Assert.That(exception.Hint, Does.Contain("--type artifact"));
    }

    [Test]
    public async Task GetHeadSubjectAsync_ReturnsTrimmedSubject()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(x => x.RunAsync("git", It.Is<IReadOnlyList<string>>(args => args[0] == "log"), "/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "Fix the header\n", string.Empty));

        Assert.That(await new Git(runner.Object).GetHeadSubjectAsync("/repo", CancellationToken.None), Is.EqualTo("Fix the header"));
    }

    [Test]
    public async Task GetHeadSubjectAsync_WithoutGit_ReturnsNull()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(x => x.RunAsync("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CliException("Could not run 'git'"));

        Assert.That(await new Git(runner.Object).GetHeadSubjectAsync("/repo", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetHeadSubjectAsync_OutsideRepository_ReturnsNull()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(x => x.RunAsync("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(128, string.Empty, "fatal: not a git repository"));

        Assert.That(await new Git(runner.Object).GetHeadSubjectAsync("/tmp", CancellationToken.None), Is.Null);
    }
}

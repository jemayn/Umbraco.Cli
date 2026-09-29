using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Infrastructure;
using Umbraco.Cli.Infrastructure.Output;
using Umbraco.Cli.Infrastructure.Processes;
using Umbraco.Cli.Tests.TestHelpers;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class InitCommandTests
{
    private const string GeneratedSecret = "GENERATED+SECRET/==";
    private static readonly Guid NewId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private string _directory = null!;
    private FakeEnvironment _environment = null!;
    private Mock<IDotnetCli> _dotnet = null!;
    private Mock<IPrompter> _prompter = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"umbraco-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "Site.csproj"), "<Project />");
        _environment = new FakeEnvironment(_directory);
        _dotnet = new Mock<IDotnetCli>();
        _dotnet.Setup(x => x.GetSdkVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("10.0.401");
        _prompter = new Mock<IPrompter>(MockBehavior.Strict);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public async Task OnPremises_WritesAppSettingsAndPrintsTheSecret()
    {
        UsePackage("Umbraco.Deploy.OnPrem");
        File.WriteAllText(AppSettingsPath, "{\n  // Keep me\n  \"Umbraco\": {}\n}");

        CliResult result = await RunAsync("deploy", "init", "--environment", "Name=Live;Type=live;Url=https://example.com", "--current-environment", "live");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string written = File.ReadAllText(AppSettingsPath);
        Assert.That(written, Does.StartWith("{\n  // Keep me\n  \"Umbraco\": {"));
        JsonNode deploy = JsonNode.Parse(written, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!["Umbraco"]!["Deploy"]!;
        Assert.That(deploy["Settings"]!["ApiSecret"]!.GetValue<string>(), Is.EqualTo(GeneratedSecret));
        Assert.That(deploy["Project"]!["CurrentWorkspaceName"]!.GetValue<string>(), Is.EqualTo("Live"));
        Assert.That(deploy["Project"]!["Workspaces"]![0]!["Id"]!.GetValue<string>(), Is.EqualTo(NewId.ToString()));
        Assert.That(result.Stdout, Does.Contain("New ApiSecret").And.Contain(GeneratedSecret));
        Assert.That(result.Stderr, Does.Contain("Detected Umbraco.Deploy.OnPrem 17.1.0 in Site.csproj."));
    }

    [Test]
    public async Task OnPremises_EnvironmentsWithoutCurrentEnvironment_FailsWhenNotInteractive()
    {
        UsePackage("Umbraco.Deploy.OnPrem");

        CliResult result = await RunAsync("deploy", "init", "--environment", "Name=Live;Type=live;Url=https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("--current-environment is required"));
        Assert.That(File.Exists(AppSettingsPath), Is.False);
    }

    [Test]
    public async Task OnPremises_EnvironmentsWithoutCurrentEnvironment_PromptsWhenInteractive()
    {
        UsePackage("Umbraco.Deploy.OnPrem");
        _environment.IsInputRedirected = false;
        _environment.IsErrorRedirected = false;
        _prompter.Setup(x => x.SelectAsync(It.IsAny<string>(), new[] { "Dev", "Live" }, It.IsAny<CancellationToken>())).ReturnsAsync("Dev");

        CliResult result = await RunAsync(
            "deploy",
            "init",
            "--no-progress",
            "-e",
            "Name=Dev;Type=development;Url=https://localhost:44307",
            "-e",
            "Name=Live;Type=live;Url=https://example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Project"]!["CurrentWorkspaceName"]!.GetValue<string>(), Is.EqualTo("Dev"));
    }

    [Test]
    public async Task OnPremises_DryRun_WritesNothingAndMasksTheSecret()
    {
        UsePackage("Umbraco.Deploy.OnPrem");

        CliResult result = await RunAsync("deploy", "init", "--dry-run", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(File.Exists(AppSettingsPath), Is.False);
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["dryRun"]!.GetValue<bool>(), Is.True);
        Assert.That(json["apiSecretGenerated"]!.GetValue<bool>(), Is.True);
        Assert.That(json["apiSecret"], Is.Null);
        Assert.That(result.Stdout, Does.Not.Contain(GeneratedSecret));
    }

    [Test]
    public async Task OnPremises_ProjectId_ReturnsUsageExitCode()
    {
        UsePackage("Umbraco.Deploy.OnPrem");

        CliResult result = await RunAsync("deploy", "init", "--project-id", Guid.NewGuid().ToString());

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
    }

    [Test]
    public async Task Cloud_WritesUmbracoCloudJsonWithoutSecretWhenApiKeyIsSet()
    {
        UsePackage("Umbraco.Deploy.Cloud");
        string cloudJson = Path.Combine(_directory, "umbraco-cloud.json");
        File.WriteAllText(cloudJson, """{ "Deploy": { "Settings": { "ApiKey": "cloud-key" } } }""");

        CliResult result = await RunAsync("deploy", "init", "-e", "Name=Local;Type=development;Url=https://localhost:44307", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode written = JsonNode.Parse(File.ReadAllText(cloudJson))!;
        Assert.That(written["Deploy"]!["Settings"]!["ApiSecret"], Is.Null);
        Assert.That(written["Deploy"]!["Project"]!["Workspaces"]![0]!["Name"]!.GetValue<string>(), Is.EqualTo("Local"));
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["package"]!.GetValue<string>(), Is.EqualTo("Umbraco.Deploy.Cloud"));
        Assert.That(json["apiSecretGenerated"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public async Task Cloud_ProjectIdFromEnvironmentVariable()
    {
        UsePackage("Umbraco.Deploy.Cloud");
        Guid projectId = Guid.NewGuid();
        _environment.Variables["UMBRACO_CLOUD_PROJECT_ID"] = projectId.ToString();

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode written = JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, "umbraco-cloud.json")))!;
        Assert.That(written["Deploy"]!["Project"]!["Id"]!.GetValue<string>(), Is.EqualTo(projectId.ToString()));
        Assert.That(written["Deploy"]!["Settings"]!["ApiSecret"]!.GetValue<string>(), Is.EqualTo(GeneratedSecret));
        Assert.That(result.Stdout, Does.Contain("--skip-preserve-umbraco-cloud-json"));
    }

    [Test]
    public async Task Cloud_CurrentEnvironment_ReturnsUsageExitCode()
    {
        UsePackage("Umbraco.Deploy.Cloud");

        CliResult result = await RunAsync("deploy", "init", "--current-environment", "Live");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
    }

    [Test]
    public async Task InvalidJson_ReturnsErrorWithoutOverwriting()
    {
        UsePackage("Umbraco.Deploy.OnPrem");
        File.WriteAllText(AppSettingsPath, "{ not json");

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Error), result.ToString());
        Assert.That(result.Stderr, Does.Contain("is not a valid JSON object"));
        Assert.That(File.ReadAllText(AppSettingsPath), Is.EqualTo("{ not json"));
    }

    [Test]
    public async Task DuplicateEnvironmentNames_ReturnsUsageExitCode()
    {
        UsePackage("Umbraco.Deploy.OnPrem");

        CliResult result = await RunAsync("deploy", "init", "-e", "Name=Live;Type=live;Url=https://a", "-e", "Name=live;Type=live;Url=https://b", "--current-environment", "Live");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("more than once"));
    }

    private string AppSettingsPath => Path.Combine(_directory, "appsettings.json");

    private void UsePackage(string packageId)
        => _dotnet
            .Setup(x => x.ListPackagesAsync(Path.Combine(_directory, "Site.csproj"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployPackageDetectorTests.PackageList(topLevel: [("Umbraco.Cms", "17.7.0"), (packageId, "17.1.0")]));

    private Task<CliResult> RunAsync(params string[] args)
        => CliRunner.RunAsync(
            _environment,
            services =>
            {
                services.AddSingleton(_dotnet.Object);
                services.AddSingleton(_prompter.Object);
                services.AddSingleton<Func<string>>(() => GeneratedSecret);
                services.AddSingleton<Func<Guid>>(() => NewId);
            },
            args);
}

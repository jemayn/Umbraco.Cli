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
    public async Task OnPremises_WritesAppSettingsWithoutPrintingTheSecret()
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
        Assert.That(result.Stdout, Does.Contain("The new ApiSecret was written to Umbraco:Deploy:Settings:ApiSecret"));
        Assert.That(result.Stdout + result.Stderr, Does.Not.Contain(GeneratedSecret));
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
    public async Task OnPremises_DryRun_WritesNothingAndNeverPrintsTheSecret()
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
        Assert.That(written["Deploy"]!["Settings"], Is.Null);
        Assert.That(result.Stdout, Does.Contain("--skip-preserve-umbraco-cloud-json"));
    }

    [Test]
    public async Task Cloud_NeverGeneratesASecret()
    {
        UsePackage("Umbraco.Deploy.Cloud");

        CliResult result = await RunAsync("deploy", "init", "-e", "Name=Local;Type=development;Url=https://localhost:44307");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        string written = File.ReadAllText(Path.Combine(_directory, "umbraco-cloud.json"));
        Assert.That(written, Does.Not.Contain("ApiSecret").And.Not.Contain(GeneratedSecret));
        Assert.That(result.Stdout, Does.Contain("Umbraco Cloud provisions it"));
    }

    [Test]
    public async Task Cloud_RotateSecret_ReturnsUsageExitCode()
    {
        UsePackage("Umbraco.Deploy.Cloud");

        CliResult result = await RunAsync("deploy", "init", "--rotate-secret");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Umbraco Cloud provisions the ApiSecret"));
        Assert.That(File.Exists(Path.Combine(_directory, "umbraco-cloud.json")), Is.False);
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

    [Test]
    public async Task OnPremises_BaselineUrlAndSecret_AreWrittenWithoutPrintingTheSecret()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        string secret = Convert.ToBase64String(new byte[64]);

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "https://baseline.example.com/", "--baseline-secret", secret, "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode settings = JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!;
        Assert.That(settings["BaselineUrl"]!.GetValue<string>(), Is.EqualTo("https://baseline.example.com"));
        Assert.That(settings["BaselineApiSecret"]!.GetValue<string>(), Is.EqualTo(secret));
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["baselineUrl"]!.GetValue<string>(), Is.EqualTo("https://baseline.example.com"));
        Assert.That(json["baselineSecretGenerated"]!.GetValue<bool>(), Is.False);
        Assert.That(json["baselineSecretConfigured"]!.GetValue<bool>(), Is.True);
        Assert.That(json["childSecretGenerated"]!.GetValue<bool>(), Is.False);
        Assert.That(json["childSecretCount"]!.GetValue<int>(), Is.Zero);
        Assert.That(result.Stdout, Does.Not.Contain(secret));
        Assert.That(result.Stderr, Does.Not.Contain("ignored"));
    }

    [Test]
    public async Task OnPremises_BaselineSecretFromEnvironmentVariable_WithBaselineUrl()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        string secret = Convert.ToBase64String(new byte[64]);
        _environment.Variables["UMBRACO_DEPLOY_BASELINE_SECRET"] = secret;

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "https://baseline.example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!["BaselineApiSecret"]!.GetValue<string>(), Is.EqualTo(secret));
    }

    [Test]
    public async Task OnPremises_BaselineSecretVariableWithoutBaselineOptions_IsIgnored()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        _environment.Variables["UMBRACO_DEPLOY_BASELINE_SECRET"] = Convert.ToBase64String(new byte[64]);

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(File.ReadAllText(AppSettingsPath), Does.Not.Contain("BaselineApiSecret"));
    }

    [Test]
    public async Task OnPremises_RotateBaselineSecret_JsonOutputNeverContainsTheSecret()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "https://baseline.example.com", "--rotate-baseline-secret", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["baselineSecretGenerated"]!.GetValue<bool>(), Is.True);
        Assert.That(json["baselineSecret"], Is.Null);
        Assert.That(result.Stdout + result.Stderr, Does.Not.Contain(GeneratedSecret));
    }

    [Test]
    public async Task OnPremises_RotateBaselineSecret_TextOutputGivesGuidanceWithoutTheSecret()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "https://baseline.example.com", "--rotate-baseline-secret");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain("The new Baseline secret was written to Umbraco:Deploy:Settings:BaselineApiSecret").And.Contain("ChildApiSecrets"));
        Assert.That(result.Stdout + result.Stderr, Does.Not.Contain(GeneratedSecret));
    }

    [Test]
    public async Task OnPremises_ChildSecret_GeneratesAndExplainsWhereToSetIt()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--child-secret", "customer-a");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!["ChildApiSecrets"]!["customer-a"]!.GetValue<string>(), Is.EqualTo(GeneratedSecret));
        Assert.That(result.Stdout, Does.Contain("The new child secret 'customer-a' was written").And.Contain("BaselineApiSecret"));
        Assert.That(result.Stdout + result.Stderr, Does.Not.Contain(GeneratedSecret));
        Assert.That(result.Stderr, Does.Contain("warning: The child secret 'customer-a' is stored in appsettings.json").And.Contain("Umbraco__Deploy__Settings__ChildApiSecrets__customer-a"));
    }

    [Test]
    public async Task OnPremises_ChildSecretDryRun_WritesNothing()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--child-secret", "customer-a", "--dry-run", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["childSecretGenerated"]!.GetValue<bool>(), Is.True);
        Assert.That(json["childSecretName"]!.GetValue<string>(), Is.EqualTo("customer-a"));
        Assert.That(json["childSecret"], Is.Null);
        Assert.That(File.Exists(AppSettingsPath), Is.False);
    }

    [Test]
    public async Task OnPremises_RemoveChildSecretAndRemoveBaseline_UpdateTheFile()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        string secret = Convert.ToBase64String(new byte[64]);
        File.WriteAllText(AppSettingsPath, $$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{secret}}", "BaselineUrl": "https://b", "BaselineApiSecret": "{{secret}}", "ChildApiSecrets": { "a": "{{secret}}" } } } } }""");

        CliResult result = await RunAsync("deploy", "init", "--remove-child-secret", "a");
        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());

        result = await RunAsync("deploy", "init", "--remove-baseline");
        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());

        JsonNode settings = JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!;
        Assert.That(settings["BaselineUrl"], Is.Null);
        Assert.That(settings["BaselineApiSecret"], Is.Null);
        Assert.That(settings["ChildApiSecrets"]!.AsObject(), Is.Empty);
    }

    [TestCase("--baseline-url", "baseline.example.com")]
    [TestCase("--baseline-url", "http://baseline.example.com")]
    [TestCase("--baseline-secret", "too-short")]
    [TestCase("--child-secret", " ")]
    [TestCase("--child-secret", "a:b")]
    [TestCase("--remove-child-secret", "a:b")]
    public async Task OnPremises_InvalidBaselineOption_ReturnsUsageExitCode(string option, string value)
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", option, value);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Not.Contain("too-short"));
        Assert.That(File.Exists(AppSettingsPath), Is.False);
    }

    [Test]
    public async Task OnPremises_BaselineUrlWithCredentials_ReturnsUsageExitCodeWithoutEchoingThem()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "https://admin:p4ssw0rd@baseline.example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("must not contain a user name or password").And.Not.Contain("p4ssw0rd"));
        Assert.That(File.Exists(AppSettingsPath), Is.False);
    }

    [TestCase("--rotate-baseline-secret", "--baseline-secret")]
    [TestCase("--remove-baseline", "--baseline-url")]
    [TestCase("--remove-baseline", "--baseline-secret")]
    [TestCase("--remove-baseline", "--rotate-baseline-secret")]
    public async Task OnPremises_ConflictingChildOptions_ReturnsUsageExitCode(string first, string second)
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        string[] args = ["deploy", "init", first, .. OptionArgs(first), second, .. OptionArgs(second)];

        CliResult result = await RunAsync(args);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("cannot be combined"));
    }

    [Test]
    public async Task OnPremises_SameChildSecretNameAddedAndRemoved_ReturnsUsageExitCode()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--child-secret", "a", "--remove-child-secret", "A");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
    }

    [Test]
    public async Task OnPremises_BaselineSecretWithoutUrl_Warns()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--baseline-secret", Convert.ToBase64String(new byte[64]));

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Contain("BaselineUrl"));
    }

    [TestCase("17.3.9")]
    [TestCase("17.0.0")]
    [TestCase("18.0.0")]
    [TestCase("18.1.5")]
    [TestCase("16.9.0")]
    public async Task OnPremises_BaselineOptionOnUnsupportedVersion_Warns(string version)
    {
        UsePackage("Umbraco.Deploy.OnPrem", version);

        CliResult result = await RunAsync("deploy", "init", "--child-secret", "a");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Contain("ignored").And.Contain(version));
    }

    [TestCase("17.4.0")]
    [TestCase("17.5.2")]
    [TestCase("17.4.0-rc1")]
    [TestCase("18.2.0")]
    [TestCase("18.3.1")]
    [TestCase("19.0.0")]
    public async Task OnPremises_BaselineOptionOnSupportedVersion_DoesNotWarn(string version)
    {
        UsePackage("Umbraco.Deploy.OnPrem", version);

        CliResult result = await RunAsync("deploy", "init", "--child-secret", "a");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stderr, Does.Not.Contain("ignored"));
    }

    [Test]
    public async Task OnPremises_NoBaselineOption_DoesNotCheckTheVersion()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.0.0");

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.Stderr, Does.Not.Contain("ignored"));
    }

    [TestCase("--baseline-url", "https://baseline.example.com")]
    [TestCase("--rotate-baseline-secret", null)]
    [TestCase("--remove-baseline", null)]
    [TestCase("--child-secret", "a")]
    [TestCase("--remove-child-secret", "a")]
    public async Task Cloud_BaselineOption_ReturnsUsageExitCode(string option, string? value)
    {
        UsePackage("Umbraco.Deploy.Cloud");

        CliResult result = await RunAsync(value is null ? ["deploy", "init", option] : ["deploy", "init", option, value]);

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("Umbraco Cloud provisions"));
        Assert.That(File.Exists(Path.Combine(_directory, "umbraco-cloud.json")), Is.False);
    }

    [Test]
    public async Task Cloud_ReportsConfiguredBaselineSettings()
    {
        UsePackage("Umbraco.Deploy.Cloud");
        string secret = Convert.ToBase64String(new byte[64]);
        File.WriteAllText(
            Path.Combine(_directory, "umbraco-cloud.json"),
            $$"""{ "Deploy": { "Settings": { "ApiSecret": "{{secret}}", "BaselineUrl": "https://baseline.example.com", "BaselineApiSecret": "{{secret}}", "ChildApiSecrets": { "a": "{{secret}}" } } } }""");

        CliResult result = await RunAsync("deploy", "init", "-o", "json");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        JsonNode json = JsonNode.Parse(result.Stdout)!;
        Assert.That(json["baselineUrl"]!.GetValue<string>(), Is.EqualTo("https://baseline.example.com"));
        Assert.That(json["baselineSecretConfigured"]!.GetValue<bool>(), Is.True);
        Assert.That(json["childSecretCount"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(result.Stdout, Does.Not.Contain(secret));

        CliResult text = await RunAsync("deploy", "init");
        Assert.That(text.Stdout, Does.Contain("https://baseline.example.com").And.Contain("1 child secret").And.Not.Contain(secret));
    }

    private static string[] OptionArgs(string option)
        => option switch
        {
            "--baseline-url" => ["https://baseline.example.com"],
            "--baseline-secret" => [Convert.ToBase64String(new byte[64])],
            _ => [],
        };

    [Test]
    public async Task OnPremises_BaselineSecretVariable_IsUsedWhenBaselineUrlIsConfigured()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        string secret = Convert.ToBase64String(new byte[64]);
        _environment.Variables["UMBRACO_DEPLOY_BASELINE_SECRET"] = secret;
        File.WriteAllText(AppSettingsPath, """{ "Umbraco": { "Deploy": { "Settings": { "BaselineUrl": "https://b.example.com" } } } }""");

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!["BaselineApiSecret"]!.GetValue<string>(), Is.EqualTo(secret));
    }

    [Test]
    public async Task OnPremises_BaselineSecretVariableWithoutBaselineUrl_IsIgnoredWithANote()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        _environment.Variables["UMBRACO_DEPLOY_BASELINE_SECRET"] = Convert.ToBase64String(new byte[64]);

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain("UMBRACO_DEPLOY_BASELINE_SECRET was ignored"));
    }

    [Test]
    public async Task OnPremises_InvalidBaselineSecretVariable_NamesTheVariableWithoutEchoingIt()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");
        _environment.Variables["UMBRACO_DEPLOY_BASELINE_SECRET"] = "too-short-secret";

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "https://baseline.example.com");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Usage), result.ToString());
        Assert.That(result.Stderr, Does.Contain("UMBRACO_DEPLOY_BASELINE_SECRET").And.Not.Contain("too-short-secret"));
    }

    [Test]
    public async Task OnPremises_LocalhostBaselineUrl_IsAccepted()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--baseline-url", "http://localhost:44307");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!["BaselineUrl"]!.GetValue<string>(), Is.EqualTo("http://localhost:44307"));
    }

    [Test]
    public async Task OnPremises_RemoveBaselineWithChildSecret_IsAllowed()
    {
        UsePackage("Umbraco.Deploy.OnPrem", "17.4.0");

        CliResult result = await RunAsync("deploy", "init", "--remove-baseline", "--child-secret", "a");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(JsonNode.Parse(File.ReadAllText(AppSettingsPath))!["Umbraco"]!["Deploy"]!["Settings"]!["ChildApiSecrets"]!["a"]!.GetValue<string>(), Is.EqualTo(GeneratedSecret));
    }

    [Test]
    public async Task Cloud_BaselineSecretVariable_DoesNotFailAPlainInit()
    {
        UsePackage("Umbraco.Deploy.Cloud");
        _environment.Variables["UMBRACO_DEPLOY_BASELINE_SECRET"] = Convert.ToBase64String(new byte[64]);

        CliResult result = await RunAsync("deploy", "init");

        Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success), result.ToString());
        Assert.That(result.Stdout, Does.Contain("UMBRACO_DEPLOY_BASELINE_SECRET was ignored").And.Contain("not used with Umbraco Deploy Cloud"));
        Assert.That(File.ReadAllText(Path.Combine(_directory, "umbraco-cloud.json")), Does.Not.Contain("Baseline"));
    }

    private string AppSettingsPath => Path.Combine(_directory, "appsettings.json");

    private void UsePackage(string packageId, string version = "17.1.0")
        => _dotnet
            .Setup(x => x.ListPackagesAsync(Path.Combine(_directory, "Site.csproj"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployPackageDetectorTests.PackageList(topLevel: [("Umbraco.Cms", "17.7.0"), (packageId, version)]));

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

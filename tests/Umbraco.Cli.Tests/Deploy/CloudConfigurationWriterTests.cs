using NUnit.Framework;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class CloudConfigurationWriterTests
{
    private const string GeneratedSecret = "GENERATED+SECRET/==";
    private static readonly Guid NewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("50b4a602-7140-4e55-8dfd-1eae73068fa0");
    private static readonly string ValidSecret = Convert.ToBase64String(new byte[64]);

    private readonly CloudConfigurationWriter _writer = new(() => GeneratedSecret, () => NewId);

    [Test]
    public void Apply_NewFile_WritesEnvironmentsProjectIdAndSecret()
    {
        var document = JsonDocumentEditor.Parse("{}");

        InitOutcome outcome = _writer.Apply(document, new InitRequest([new("Live", "live", "https://live.example.com", null)], null, ProjectId, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo($$"""
            {
              "Deploy": {
                "Project": {
                  "Workspaces": [
                    {
                      "Id": "{{NewId}}",
                      "Name": "Live",
                      "Type": "live",
                      "Url": "https://live.example.com"
                    }
                  ],
                  "Id": "{{ProjectId}}"
                },
                "Settings": {
                  "ApiSecret": "{{GeneratedSecret}}"
                }
              }
            }
            """.ReplaceLineEndings("\n")));
        Assert.That(outcome.Notes, Has.Some.Contains("--skip-preserve-umbraco-cloud-json"));
    }

    [Test]
    public void Apply_ApiKey_WritesNoSecret()
    {
        var document = JsonDocumentEditor.Parse("""{ "Deploy": { "Settings": { "ApiKey": "cloud-api-key" } } }""");
        string original = document.ToString();

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo(original));
        Assert.That(outcome.GeneratedApiSecret, Is.Null);
        Assert.That(outcome.Notes, Has.Some.Contains("an ApiKey is configured"));
        Assert.That(outcome.Warnings, Is.Empty);
    }

    [Test]
    public void Apply_ApiKeyWithRotate_WarnsAndWritesNoSecret()
    {
        var document = JsonDocumentEditor.Parse("""{ "Deploy": { "Settings": { "ApiKey": "cloud-api-key" } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: true));

        Assert.That(document.GetString("Deploy", "Settings", "ApiSecret"), Is.Null);
        Assert.That(document.GetString("Deploy", "Settings", "ApiKey"), Is.EqualTo("cloud-api-key"));
        Assert.That(outcome.Warnings.Single(), Does.Contain("--rotate-secret has no effect"));
    }

    [Test]
    public void Apply_ValidSecret_KeepsItUnlessRotated()
    {
        var kept = JsonDocumentEditor.Parse($$"""{ "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } }""");
        var rotated = JsonDocumentEditor.Parse($$"""{ "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } }""");

        _writer.Apply(kept, new InitRequest(null, null, null, RotateSecret: false));
        _writer.Apply(rotated, new InitRequest(null, null, null, RotateSecret: true));

        Assert.That(kept.GetString("Deploy", "Settings", "ApiSecret"), Is.EqualTo(ValidSecret));
        Assert.That(rotated.GetString("Deploy", "Settings", "ApiSecret"), Is.EqualTo(GeneratedSecret));
    }

    [Test]
    public void Apply_DifferentExistingProjectId_KeepsItAndWarns()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Deploy": { "Project": { "Id": "{{Guid.Empty}}" }, "Settings": { "ApiSecret": "{{ValidSecret}}" } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, ProjectId, RotateSecret: false));

        Assert.That(document.GetString("Deploy", "Project", "Id"), Is.EqualTo(Guid.Empty.ToString()));
        Assert.That(outcome.Warnings.Single(), Does.Contain("already set"));
    }

    [Test]
    public void Apply_ExampleFile_OnlyChangesTheWorkspaces()
    {
        const string original = """
            {
              "Deploy": {
                "Project": {
                  "Id": "50b4a602-7140-4e55-8dfd-1eae73068fa0",
                  "Workspaces": [
                    {
                      "Id": "efef5e89-a19b-434b-b68a-26e022a0ad52",
                      "Name": "Live",
                      "Type": "live",
                      "Url": "https://localhost:44307"
                    }
                  ]
                },
                "Settings": {
                  "ApiKey": "api-key"
                }
              },
              "Identity": {
                "ClientId": "00000000-0000-0000-0000-000000000000"
              }
            }
            """;
        var document = JsonDocumentEditor.Parse(original);

        _writer.Apply(document, new InitRequest([new("Live", "live", "https://localhost:44307", null), new("Dev", "development", "https://localhost:44308", null)], null, ProjectId, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo(original.Replace(
            """
                    }
                  ]
            """,
            $$"""
                    },
                    {
                      "Id": "{{NewId}}",
                      "Name": "Dev",
                      "Type": "development",
                      "Url": "https://localhost:44308"
                    }
                  ]
            """,
            StringComparison.Ordinal)));
    }

    [Test]
    public void Apply_BaselineSettings_AreReportedAndNeverWritten()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "BaselineUrl": "https://baseline.example.com", "BaselineApiSecret": "{{ValidSecret}}", "ChildApiSecrets": { "a": "{{ValidSecret}}", "b": "{{ValidSecret}}" } } } }""");
        string original = document.ToString();

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo(original));
        Assert.That(outcome.BaselineUrl, Is.EqualTo("https://baseline.example.com"));
        Assert.That(outcome.BaselineSecretConfigured, Is.True);
        Assert.That(outcome.ChildSecretCount, Is.EqualTo(2));
        Assert.That(outcome.Notes, Has.Some.Contains("https://baseline.example.com"));
        Assert.That(outcome.Notes, Has.Some.Contains("2 child secrets"));
        Assert.That(outcome.Notes, Has.Some.Contains("per project"));
        Assert.That(outcome.Notes, Has.Some.Contains("provisions"));
    }

    [Test]
    public void Apply_ObsoleteUpstream_IsReportedAsBaselineUrl()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" }, "Project": { "Upstream": "https://upstream.example.com" } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(outcome.BaselineUrl, Is.EqualTo("https://upstream.example.com"));
        Assert.That(outcome.BaselineSecretConfigured, Is.False);
        Assert.That(outcome.ChildSecretCount, Is.Zero);
    }

    [Test]
    public void Apply_NoBaselineSettings_ReportsNothing()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(outcome.BaselineUrl, Is.Null);
        Assert.That(outcome.Notes, Has.None.Contains("Baseline"));
    }
}

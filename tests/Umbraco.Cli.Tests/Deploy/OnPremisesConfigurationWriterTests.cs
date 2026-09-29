using NUnit.Framework;
using Umbraco.Cli.Deploy.Configuration;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class OnPremisesConfigurationWriterTests
{
    private const string GeneratedSecret = "GENERATED+SECRET/==";
    private static readonly Guid NewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly string ValidSecret = Convert.ToBase64String(new byte[64]);

    private readonly OnPremisesConfigurationWriter _writer = new(() => GeneratedSecret, () => NewId);

    [Test]
    public void Apply_NewSite_WritesSecretEnvironmentsAndCurrentEnvironmentKeepingComments()
    {
        var document = JsonDocumentEditor.Parse("""
            {
              "$schema": "appsettings-schema.json",
              // Umbraco settings
              "Umbraco": {
                "CMS": {}
              }
            }
            """);

        InitOutcome outcome = _writer.Apply(document, new InitRequest([new("Live", "live", "https://example.com", null)], "Live", null, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo($$"""
            {
              "$schema": "appsettings-schema.json",
              // Umbraco settings
              "Umbraco": {
                "CMS": {},
                "Deploy": {
                  "Project": {
                    "Workspaces": [
                      {
                        "Id": "{{NewId}}",
                        "Name": "Live",
                        "Type": "live",
                        "Url": "https://example.com"
                      }
                    ],
                    "CurrentWorkspaceName": "Live"
                  },
                  "Settings": {
                    "ApiSecret": "{{GeneratedSecret}}"
                  }
                }
              }
            }
            """));
        Assert.That(outcome.GeneratedApiSecret, Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.Changes, Has.Count.EqualTo(3));
    }

    [Test]
    public void Apply_ValidSecret_KeepsIt()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } } }""");
        string original = document.ToString();

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo(original));
        Assert.That(outcome.GeneratedApiSecret, Is.Null);
        Assert.That(outcome.Changes, Is.Empty);
    }

    [Test]
    public void Apply_ValidSecretWithRotate_GeneratesNewSecret()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: true));

        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "ApiSecret"), Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.GeneratedApiSecret, Is.EqualTo(GeneratedSecret));
    }

    [Test]
    public void Apply_InvalidSecret_RegeneratesIt()
    {
        var document = JsonDocumentEditor.Parse("""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "too-short" } } } }""");

        _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "ApiSecret"), Is.EqualTo(GeneratedSecret));
    }

    [Test]
    public void Apply_ApiKey_KeepsItAndWarns()
    {
        var document = JsonDocumentEditor.Parse("""{ "Umbraco": { "Deploy": { "Settings": { "ApiKey": "old-api-key" } } } }""");
        string original = document.ToString();

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: false));

        Assert.That(document.ToString(), Is.EqualTo(original));
        Assert.That(outcome.GeneratedApiSecret, Is.Null);
        Assert.That(outcome.Warnings.Single(), Does.Contain("--rotate-secret"));
    }

    [Test]
    public void Apply_ApiKeyWithRotate_ReplacesItWithApiSecret()
    {
        var document = JsonDocumentEditor.Parse("""
            {
              "Umbraco": {
                "Deploy": {
                  "Settings": {
                    "ApiKey": "old-api-key",
                    "Edition": "Default"
                  }
                }
              }
            }
            """);

        _writer.Apply(document, new InitRequest(null, null, null, RotateSecret: true));

        Assert.That(document.ToString(), Is.EqualTo($$"""
            {
              "Umbraco": {
                "Deploy": {
                  "Settings": {
                    "Edition": "Default",
                    "ApiSecret": "{{GeneratedSecret}}"
                  }
                }
              }
            }
            """));
    }

    [Test]
    public void Apply_ExistingEnvironment_ReusesItsIdAndReplacesTheList()
    {
        var document = JsonDocumentEditor.Parse($$"""
            {
              "Umbraco": {
                "Deploy": {
                  "Settings": { "ApiSecret": "{{ValidSecret}}" },
                  "Project": {
                    "CurrentWorkspaceName": "Live",
                    "Workspaces": [
                      { "Id": "efef5e89-a19b-434b-b68a-26e022a0ad52", "Name": "Live", "Type": "live", "Url": "https://old" },
                      { "Id": "22222222-2222-2222-2222-222222222222", "Name": "Removed", "Type": "staging", "Url": "https://removed" }
                    ]
                  }
                }
              }
            }
            """);

        InitOutcome outcome = _writer.Apply(document, new InitRequest([new("live", "live", "https://new", null)], null, null, RotateSecret: false));

        Assert.That(outcome.Environments, Is.EqualTo(new[] { new WorkspaceEntry(Guid.Parse("efef5e89-a19b-434b-b68a-26e022a0ad52"), "live", "live", "https://new") }));
        Assert.That(WorkspaceList.Read(document.GetNode("Umbraco", "Deploy", "Project", "Workspaces")), Is.EqualTo(outcome.Environments));
        Assert.That(document.GetString("Umbraco", "Deploy", "Project", "CurrentWorkspaceName"), Is.EqualTo("Live"));
    }
}

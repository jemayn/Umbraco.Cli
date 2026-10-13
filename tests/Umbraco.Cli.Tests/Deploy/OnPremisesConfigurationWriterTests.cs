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

    [Test]
    public void Apply_BaselineUrlAndSecret_WritesBoth()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(Url: "https://baseline.example.com", Secret: ValidSecret)));

        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "BaselineUrl"), Is.EqualTo("https://baseline.example.com"));
        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "BaselineApiSecret"), Is.EqualTo(ValidSecret));
        Assert.That(outcome.GeneratedBaselineSecret, Is.Null);
        Assert.That(outcome.BaselineUrl, Is.EqualTo("https://baseline.example.com"));
        Assert.That(outcome.BaselineSecretConfigured, Is.True);
        Assert.That(outcome.Warnings, Is.Empty);
    }

    [Test]
    public void Apply_RotateBaselineSecret_GeneratesIt()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "BaselineUrl": "https://b", "BaselineApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(RotateSecret: true)));

        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "BaselineApiSecret"), Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.GeneratedBaselineSecret, Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.GeneratedApiSecret, Is.Null);
    }

    [Test]
    public void Apply_BaselineUrlOnly_KeepsTheExistingBaselineSecret()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "BaselineUrl": "https://b", "BaselineApiSecret": "{{ValidSecret}}" } } } }""");
        string original = document.ToString();

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(Url: "https://b")));

        Assert.That(document.ToString(), Is.EqualTo(original));
        Assert.That(outcome.GeneratedBaselineSecret, Is.Null);
        Assert.That(outcome.BaselineSecretConfigured, Is.True);
    }

    [Test]
    public void Apply_RemoveBaseline_RemovesUrlAndSecret()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "BaselineUrl": "https://b", "BaselineApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(Remove: true)));

        Assert.That(document.GetNode("Umbraco", "Deploy", "Settings", "BaselineUrl"), Is.Null);
        Assert.That(document.GetNode("Umbraco", "Deploy", "Settings", "BaselineApiSecret"), Is.Null);
        Assert.That(outcome.BaselineUrl, Is.Null);
        Assert.That(outcome.BaselineSecretConfigured, Is.False);
        Assert.That(outcome.Changes, Has.Some.Contains("Removed"));
        Assert.That(outcome.Warnings, Is.Empty);
    }

    [Test]
    public void Apply_BaselineSecretWithoutUrl_Warns()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(Secret: ValidSecret)));

        Assert.That(outcome.Warnings.Single(), Does.Contain("BaselineUrl"));
    }

    [Test]
    public void Apply_ChildSecret_GeneratesNamedEntry()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(ChildSecretName: "customer-a")));

        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "ChildApiSecrets", "customer-a"), Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.GeneratedChildSecret, Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.GeneratedChildSecretName, Is.EqualTo("customer-a"));
        Assert.That(outcome.ChildSecretCount, Is.EqualTo(1));
        Assert.That(outcome.Warnings.Single(), Does.Contain("don't commit it").And.Contain("Umbraco__Deploy__Settings__ChildApiSecrets__customer-a"));
    }

    [Test]
    public void Apply_ExistingChildSecret_IsKeptWithANote()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "ChildApiSecrets": { "customer-a": "{{ValidSecret}}" } } } } }""");
        string original = document.ToString();

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(ChildSecretName: "Customer-A")));

        Assert.That(document.ToString(), Is.EqualTo(original));
        Assert.That(outcome.GeneratedChildSecret, Is.Null);
        Assert.That(outcome.Notes, Has.Some.Contains("customer-a-1"));
    }

    [Test]
    public void Apply_InvalidChildSecret_RegeneratesIt()
    {
        var document = JsonDocumentEditor.Parse("""{ "Umbraco": { "Deploy": { "Settings": { "ChildApiSecrets": { "customer-a": "short" } } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(ChildSecretName: "customer-a")));

        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "ChildApiSecrets", "customer-a"), Is.EqualTo(GeneratedSecret));
        Assert.That(outcome.GeneratedChildSecretName, Is.EqualTo("customer-a"));
    }

    [Test]
    public void Apply_RemoveChildSecret_RemovesOnlyThatEntry()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "ChildApiSecrets": { "a": "{{ValidSecret}}", "b": "{{ValidSecret}}" } } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(RemoveChildSecretName: "A")));

        Assert.That(document.GetNode("Umbraco", "Deploy", "Settings", "ChildApiSecrets", "a"), Is.Null);
        Assert.That(document.GetString("Umbraco", "Deploy", "Settings", "ChildApiSecrets", "b"), Is.EqualTo(ValidSecret));
        Assert.That(outcome.ChildSecretCount, Is.EqualTo(1));
        Assert.That(outcome.Changes, Has.Some.Contains("Removed"));
    }

    [Test]
    public void Apply_RemoveMissingChildSecret_NotesIt()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(RemoveChildSecretName: "a")));

        Assert.That(outcome.Changes, Is.Empty);
        Assert.That(outcome.Notes, Has.Some.Contains("no child secret"));
    }

    [Test]
    public void Apply_EmptyBaselineUrl_IsReportedAsNull()
    {
        var document = JsonDocumentEditor.Parse($$"""{ "Umbraco": { "Deploy": { "Settings": { "ApiSecret": "{{ValidSecret}}", "BaselineUrl": "" } } } }""");

        InitOutcome outcome = _writer.Apply(document, new InitRequest(null, null, null, false, new BaselineOptions(ChildSecretName: "a")));

        Assert.That(outcome.BaselineUrl, Is.Null);
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Umbraco.Cli.Infrastructure.Json;

namespace Umbraco.Cli.Tests.Infrastructure;

[TestFixture]
public class JsonDocumentEditorTests
{
    private const string AppSettings = """
        {
          "$schema": "appsettings-schema.json",
          // Logging configuration
          "Serilog": {
            "MinimumLevel": {
              "Default": "Information"
            }
          },
          "Umbraco": {
            "CMS": {
              /* Keep this */
              "Global": {
                "Id": "abc"
              }
            }
          }
        }
        """;

    [Test]
    public void ToString_WithoutEdits_ReturnsContentUnchanged()
    {
        var editor = JsonDocumentEditor.Parse(AppSettings);

        Assert.That(editor.ToString(), Is.EqualTo(AppSettings));
    }

    [Test]
    public void Parse_WithRootArray_Throws()
        => Assert.Throws<JsonException>(() => JsonDocumentEditor.Parse("[1, 2]"));

    [Test]
    public void Parse_WithInvalidJson_Throws()
        => Assert.That(() => JsonDocumentEditor.Parse("{ \"a\": }"), Throws.InstanceOf<JsonException>());

    [Test]
    public void GetString_IsCaseInsensitiveAndIgnoresComments()
    {
        var editor = JsonDocumentEditor.Parse(AppSettings);

        Assert.That(editor.GetString("umbraco", "cms", "global", "id"), Is.EqualTo("abc"));
        Assert.That(editor.GetString("Umbraco", "Missing", "Id"), Is.Null);
        Assert.That(editor.GetString("Serilog"), Is.Null);
    }

    [Test]
    public void GetNode_ReturnsObjectsAndArrays()
    {
        var editor = JsonDocumentEditor.Parse("""{ "a": [1, /* two */ 2], "b": { "c": true } }""");

        Assert.That(editor.GetNode("a")!.AsArray().Count, Is.EqualTo(2));
        Assert.That(editor.GetNode("b", "c")!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void Set_ExistingValue_ReplacesOnlyThatValue()
    {
        var editor = JsonDocumentEditor.Parse(AppSettings);

        editor.Set(["Umbraco", "CMS", "Global", "Id"], "xyz");

        Assert.That(editor.ToString(), Is.EqualTo(AppSettings.Replace("\"abc\"", "\"xyz\"", StringComparison.Ordinal)));
    }

    [Test]
    public void Set_MissingPath_InsertsNestedObjectsWithDetectedIndentation()
    {
        var editor = JsonDocumentEditor.Parse(AppSettings);

        editor.Set(["Umbraco", "Deploy", "Settings", "ApiSecret"], "a+b/c==");

        const string expected = """
            {
              "$schema": "appsettings-schema.json",
              // Logging configuration
              "Serilog": {
                "MinimumLevel": {
                  "Default": "Information"
                }
              },
              "Umbraco": {
                "CMS": {
                  /* Keep this */
                  "Global": {
                    "Id": "abc"
                  }
                },
                "Deploy": {
                  "Settings": {
                    "ApiSecret": "a+b/c=="
                  }
                }
              }
            }
            """;
        Assert.That(editor.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Set_Array_WritesIndentedArray()
    {
        var editor = JsonDocumentEditor.Parse("{\n    \"Deploy\": {}\n}");

        editor.Set(["Deploy", "Workspaces"], new JsonArray(new JsonObject { ["Name"] = "Live" }));

        Assert.That(editor.ToString(), Is.EqualTo("{\n    \"Deploy\": {\n        \"Workspaces\": [\n            {\n                \"Name\": \"Live\"\n            }\n        ]\n    }\n}"));
    }

    [Test]
    public void Set_ReplacingArray_KeepsSurroundingComments()
    {
        const string original = "{\n  // Environments\n  \"Workspaces\": [ { \"Name\": \"Old\" } ], // trailing\n  \"Other\": 1\n}";
        var editor = JsonDocumentEditor.Parse(original);

        editor.Set(["Workspaces"], new JsonArray());

        Assert.That(editor.ToString(), Is.EqualTo("{\n  // Environments\n  \"Workspaces\": [], // trailing\n  \"Other\": 1\n}"));
    }

    [Test]
    public void Set_AfterLastMemberWithLineComment_AddsCommaBeforeComment()
    {
        var editor = JsonDocumentEditor.Parse("{\n  \"a\": 1 // one\n}");

        editor.Set(["b"], 2);

        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"a\": 1, // one\n  \"b\": 2\n}"));
    }

    [Test]
    public void Set_AfterTrailingComma_DoesNotAddAnotherComma()
    {
        var editor = JsonDocumentEditor.Parse("{\n  \"a\": 1,\n}");

        editor.Set(["b"], 2);

        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"a\": 1,\n  \"b\": 2\n}"));
    }

    [Test]
    public void Set_InEmptyObject_PutsClosingBraceOnItsOwnLine()
    {
        var editor = JsonDocumentEditor.Parse("{}");

        editor.Set(["Deploy", "Settings", "ApiSecret"], "secret");

        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"Deploy\": {\n    \"Settings\": {\n      \"ApiSecret\": \"secret\"\n    }\n  }\n}"));
    }

    [Test]
    public void Set_OnSingleLineObject_KeepsItValid()
    {
        var editor = JsonDocumentEditor.Parse("""{"a": 1}""");

        editor.Set(["b"], 2);

        Assert.That(JsonNode.Parse(editor.ToString())!["b"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(editor.GetString("a"), Is.Null);
        Assert.That(editor.GetNode("a")!.GetValue<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Set_WithTabsAndCrLf_UsesTheSameFormatting()
    {
        var editor = JsonDocumentEditor.Parse("{\r\n\t\"a\": {\r\n\t\t\"b\": 1\r\n\t}\r\n}");

        editor.Set(["a", "c"], new JsonObject { ["d"] = 2 });

        Assert.That(editor.ToString(), Is.EqualTo("{\r\n\t\"a\": {\r\n\t\t\"b\": 1,\r\n\t\t\"c\": {\r\n\t\t\t\"d\": 2\r\n\t\t}\r\n\t}\r\n}"));
    }

    [Test]
    public void Set_WhenParentIsNotAnObject_Throws()
    {
        var editor = JsonDocumentEditor.Parse("""{ "Deploy": "text" }""");

        Assert.Throws<InvalidOperationException>(() => editor.Set(["Deploy", "Settings"], 1));
    }

    [Test]
    public void Set_WithDuplicateNames_ReplacesTheLastOne()
    {
        var editor = JsonDocumentEditor.Parse("""{ "a": 1, "A": 2 }""");

        editor.Set(["a"], 3);

        Assert.That(editor.ToString(), Is.EqualTo("""{ "a": 1, "A": 3 }"""));
    }

    [Test]
    public void Remove_MiddleMember_RemovesItsLine()
    {
        var editor = JsonDocumentEditor.Parse("{\n  \"a\": 1,\n  \"b\": 2,\n  \"c\": 3\n}");

        bool removed = editor.Remove("b");

        Assert.That(removed, Is.True);
        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"a\": 1,\n  \"c\": 3\n}"));
    }

    [Test]
    public void Remove_LastMember_RemovesPrecedingComma()
    {
        var editor = JsonDocumentEditor.Parse("{\n  \"a\": 1,\n  \"b\": 2\n}");

        editor.Remove("b");

        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"a\": 1\n}"));
    }

    [Test]
    public void Remove_OnlyMember_LeavesEmptyObject()
    {
        var editor = JsonDocumentEditor.Parse("{ \"s\": { \"ApiKey\": \"x\" } }");

        editor.Remove("s", "ApiKey");

        Assert.That(editor.ToString(), Is.EqualTo("{ \"s\": {} }"));
    }

    [Test]
    public void Remove_LastMember_KeepsCommentsOfThePreviousMember()
    {
        var editor = JsonDocumentEditor.Parse("{\n  \"ApiSecret\": \"s\", // shared\n  /* old */\n  \"ApiKey\": \"k\"\n}");

        editor.Remove("ApiKey");

        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"ApiSecret\": \"s\" // shared\n  /* old */\n}"));
    }

    [Test]
    public void Remove_LastMemberWithTrailingComma_KeepsThePreviousComma()
    {
        var editor = JsonDocumentEditor.Parse("{\n  \"a\": 1,\n  \"b\": 2,\n}");

        editor.Remove("b");

        Assert.That(editor.ToString(), Is.EqualTo("{\n  \"a\": 1,\n}"));
    }

    [Test]
    public void Remove_OnlyMember_KeepsCommentsInTheObject()
    {
        var editor = JsonDocumentEditor.Parse("{\n  // keep\n  \"ApiKey\": \"x\"\n}");

        editor.Remove("ApiKey");

        Assert.That(editor.ToString(), Is.EqualTo("{\n  // keep\n}"));
    }

    [Test]
    public void Remove_MemberSharingALine_RemovesOnlyThatMember()
    {
        var editor = JsonDocumentEditor.Parse("""{ "a": 1, "b": 2, "c": 3 }""");

        editor.Remove("b");
        editor.Remove("c");

        Assert.That(JsonNode.Parse(editor.ToString())!.ToJsonString(), Is.EqualTo("""{"a":1}"""));
    }

    [Test]
    public void Remove_MissingMember_ReturnsFalse()
    {
        var editor = JsonDocumentEditor.Parse(AppSettings);

        Assert.That(editor.Remove("Umbraco", "Deploy", "ApiKey"), Is.False);
        Assert.That(editor.ToString(), Is.EqualTo(AppSettings));
    }

    [Test]
    public void ToBytes_KeepsByteOrderMark()
    {
        byte[] content = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{\n  \"a\": 1\n}")];
        var editor = JsonDocumentEditor.Parse(content);

        editor.Set(["a"], 2);

        Assert.That(editor.ToBytes(), Is.EqualTo((byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{\n  \"a\": 2\n}")]));
    }

    [Test]
    public void Set_NonAsciiText_IsWrittenAsUtf8()
    {
        var editor = JsonDocumentEditor.Parse("{ \"name\": \"x\" }");

        editor.Set(["name"], "Æblegrød");

        Assert.That(editor.ToString(), Is.EqualTo("{ \"name\": \"Æblegrød\" }"));
    }
}

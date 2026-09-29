using System.Text.Json.Nodes;
using NUnit.Framework;
using Umbraco.Cli.Deploy.Configuration;

namespace Umbraco.Cli.Tests.Deploy;

[TestFixture]
public class WorkspaceListTests
{
    private static readonly Guid LiveId = Guid.Parse("efef5e89-a19b-434b-b68a-26e022a0ad52");
    private static readonly Guid NewId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Test]
    public void Read_IgnoresEntriesWithoutNameAndPropertyNameCasing()
    {
        JsonNode workspaces = JsonNode.Parse($$"""[ { "id": "{{LiveId}}", "name": "Live", "type": "live", "url": "https://live" }, { "Type": "x" } ]""")!;

        IReadOnlyList<WorkspaceEntry> entries = WorkspaceList.Read(workspaces);

        Assert.That(entries, Is.EqualTo(new[] { new WorkspaceEntry(LiveId, "Live", "live", "https://live") }));
    }

    [Test]
    public void Build_ReusesIdOfEntryWithSameName()
    {
        WorkspaceEntry[] existing = [new(LiveId, "Live", "live", "https://old")];
        EnvironmentSpec[] environments = [new("LIVE", "live", "https://new", null), new("Dev", "development", "https://dev", null)];

        IReadOnlyList<WorkspaceEntry> entries = WorkspaceList.Build(existing, environments, () => NewId);

        Assert.That(entries, Is.EqualTo(new[]
        {
            new WorkspaceEntry(LiveId, "LIVE", "live", "https://new"),
            new WorkspaceEntry(NewId, "Dev", "development", "https://dev"),
        }));
    }

    [Test]
    public void Build_GivenIdWins()
    {
        WorkspaceEntry[] existing = [new(LiveId, "Live", "live", "https://old")];

        IReadOnlyList<WorkspaceEntry> entries = WorkspaceList.Build(existing, [new("Live", "live", "https://new", NewId)], Guid.NewGuid);

        Assert.That(entries.Single().Id, Is.EqualTo(NewId));
    }

    [Test]
    public void ToJson_UsesUmbracoDeployPropertyNames()
    {
        JsonArray json = WorkspaceList.ToJson([new WorkspaceEntry(LiveId, "Live", "live", "https://live")]);

        Assert.That(json.ToJsonString(), Is.EqualTo($$"""[{"Id":"{{LiveId}}","Name":"Live","Type":"live","Url":"https://live"}]"""));
    }
}

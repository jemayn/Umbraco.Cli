using System.Xml;
using System.Xml.Linq;

namespace Umbraco.Cli.Cloud.Init;

/// <summary>
/// Edits an <c>.slnx</c> solution file (<c>dotnet sln</c> can only add projects, not files).
/// </summary>
internal static class SolutionFile
{
    public const string SolutionItemsFolder = "/Solution Items/";

    /// <summary>
    /// Adds files (relative to the solution's directory) to the Solution Items folder, creating it as the first folder when missing.
    /// </summary>
    public static void AddSolutionItems(string slnxPath, IEnumerable<string> relativeFiles)
    {
        XDocument document = XDocument.Load(slnxPath, LoadOptions.None);
        XElement solution = document.Root ?? throw new InvalidOperationException($"'{slnxPath}' has no Solution element.");

        XElement? folder = solution.Elements("Folder").FirstOrDefault(element => (string?)element.Attribute("Name") == SolutionItemsFolder);
        if (folder is null)
        {
            folder = new XElement("Folder", new XAttribute("Name", SolutionItemsFolder));
            solution.AddFirst(folder);
        }

        foreach (string file in relativeFiles.Select(file => file.Replace('\\', '/')).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (folder.Elements("File").Any(element => (string?)element.Attribute("Path") == file) is false)
            {
                folder.Add(new XElement("File", new XAttribute("Path", file)));
            }
        }

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using (XmlWriter writer = XmlWriter.Create(slnxPath, settings))
        {
            document.Save(writer);
        }

        File.AppendAllText(slnxPath, "\n");
    }
}

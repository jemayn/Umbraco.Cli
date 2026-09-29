using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Umbraco.Cli.Infrastructure.Json;

/// <summary>
/// Edits a JSON (with comments) document in place: only the edited values change, so comments, property order and formatting are preserved.
/// </summary>
/// <remarks>
/// Property names are matched case-insensitively (like .NET configuration keys). When a name occurs more than once in an object, the last one is used (like .NET configuration).
/// </remarks>
internal sealed class JsonDocumentEditor
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Allow,
        AllowTrailingCommas = true,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly bool _hasBom;
    private readonly string _newLine;
    private readonly string _indentUnit;
    private byte[] _utf8;

    private JsonDocumentEditor(byte[] utf8, bool hasBom)
    {
        _utf8 = utf8;
        _hasBom = hasBom;
        string text = Encoding.UTF8.GetString(utf8);
        _newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        _indentUnit = DetectIndentUnit(text);
    }

    /// <summary>
    /// Parses the content of a JSON file (optionally starting with a UTF-8 BOM) that must contain an object.
    /// </summary>
    /// <exception cref="JsonException">The content is not valid JSON or its root is not an object.</exception>
    public static JsonDocumentEditor Parse(byte[] content)
    {
        bool hasBom = content.AsSpan().StartsWith(Utf8Bom);
        byte[] utf8 = hasBom ? content[Utf8Bom.Length..] : content;

        using (JsonDocument document = JsonDocument.Parse(utf8, DocumentOptions))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The root of the document must be an object.");
            }
        }

        return new JsonDocumentEditor(utf8, hasBom);
    }

    public static JsonDocumentEditor Parse(string content) => Parse(Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Gets the value at <paramref name="path" />, or <c>null</c> if it (or one of its parents) does not exist.
    /// </summary>
    public JsonNode? GetNode(params string[] path)
    {
        MemberSpan? member = FindMember(path);
        return member is null ? null : JsonNode.Parse(_utf8.AsSpan(member.ValueStart, member.ValueEnd - member.ValueStart), documentOptions: DocumentOptions);
    }

    /// <summary>
    /// Gets the string value at <paramref name="path" />, or <c>null</c> if it does not exist or is not a string.
    /// </summary>
    public string? GetString(params string[] path)
        => GetNode(path) is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>
    /// Sets the value at <paramref name="path" />, replacing an existing value or inserting the property (and any missing parent objects).
    /// </summary>
    public void Set(string[] path, JsonNode? value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(path.Length);

        ObjectSpan parent = EnsureObject(path[..^1]);
        MemberSpan? member = parent.Find(path[^1]);
        if (member is null)
        {
            InsertMember(parent, path[^1], value);
        }
        else
        {
            string indent = LineIndentation(member.NameStart);
            Replace(member.ValueStart, member.ValueEnd, Serialize(value, indent));
        }
    }

    /// <summary>
    /// Removes the property at <paramref name="path" />.
    /// </summary>
    /// <returns><c>true</c> if the property existed.</returns>
    public bool Remove(params string[] path)
    {
        ArgumentOutOfRangeException.ThrowIfZero(path.Length);

        ObjectSpan? parent = FindObject(path[..^1]);
        int index = parent?.IndexOf(path[^1]) ?? -1;
        if (parent is null || index < 0)
        {
            return false;
        }

        MemberSpan member = parent.Members[index];
        int nextNameStart = index + 1 < parent.Members.Count ? parent.Members[index + 1].NameStart : parent.End;
        int trailingComma = FindComma(member.ValueEnd, nextNameStart);

        // Remove only the member (and its line, when it has one of its own), so comments around it are kept
        int lineStart = LineStart(member.NameStart);
        int start = IsWhitespace(lineStart, member.NameStart) ? lineStart : member.NameStart;
        int end = trailingComma >= 0 ? trailingComma + 1 : member.ValueEnd;
        int lineEnd = SkipSpaces(end);
        if (start == lineStart && StartsNewLine(lineEnd))
        {
            end = lineEnd + (_utf8[lineEnd] == '\r' ? 2 : 1);
        }

        Replace(start, end, string.Empty);

        // Without a comma of its own, the member was the last one, so the comma separating it from the previous member goes too
        if (trailingComma < 0 && index > 0)
        {
            int comma = FindComma(parent.Members[index - 1].ValueEnd, start);
            if (comma >= 0)
            {
                Replace(comma, comma + 1, string.Empty);
            }
        }

        // Collapse an object that only has whitespace left
        ObjectSpan updated = ReadObject(parent.Start);
        if (updated.Members.Count == 0 && IsWhitespaceOnly(updated.Start + 1, updated.End))
        {
            Replace(updated.Start + 1, updated.End, string.Empty);
        }

        return true;
    }

    /// <summary>
    /// Gets the document content, including the UTF-8 BOM if the parsed content had one.
    /// </summary>
    public byte[] ToBytes() => _hasBom ? [.. Utf8Bom, .. _utf8] : [.. _utf8];

    public override string ToString() => Encoding.UTF8.GetString(_utf8);

    private static string DetectIndentUnit(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            int length = 0;
            while (length < line.Length && (line[length] == ' ' || line[length] == '\t'))
            {
                length++;
            }

            if (length > 0 && length < line.TrimEnd('\r').Length)
            {
                return line[0] == '\t' ? "\t" : new string(' ', length);
            }
        }

        return "  ";
    }

    private ObjectSpan EnsureObject(string[] path)
    {
        ObjectSpan current = ReadObject(RootStart());
        foreach (string name in path)
        {
            MemberSpan? member = current.Find(name);
            if (member is null)
            {
                InsertMember(current, name, new JsonObject());
                member = ReadObject(current.Start).Find(name)!;
            }

            if (_utf8[member.ValueStart] != '{')
            {
                throw new InvalidOperationException($"Expected '{name}' to be an object.");
            }

            current = ReadObject(member.ValueStart);
        }

        return current;
    }

    private ObjectSpan? FindObject(string[] path)
    {
        ObjectSpan current = ReadObject(RootStart());
        foreach (string name in path)
        {
            MemberSpan? member = current.Find(name);
            if (member is null || _utf8[member.ValueStart] != '{')
            {
                return null;
            }

            current = ReadObject(member.ValueStart);
        }

        return current;
    }

    private MemberSpan? FindMember(string[] path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        return FindObject(path[..^1])?.Find(path[^1]);
    }

    private void InsertMember(ObjectSpan parent, string name, JsonNode? value)
    {
        string closeIndent = LineIndentation(parent.Start);
        string memberIndent = parent.Members.Count > 0 ? LineIndentation(parent.Members[^1].NameStart) : closeIndent + _indentUnit;
        string memberText = _newLine + memberIndent + Serialize(JsonValue.Create(name), memberIndent) + ": " + Serialize(value, memberIndent);

        // Insert after the last content (a value, trailing comma or comment) and keep the closing brace on its own line
        int insertAt = parent.End;
        while (insertAt > parent.Start + 1 && IsWhitespaceByte(_utf8[insertAt - 1]))
        {
            insertAt--;
        }

        string gap = Encoding.UTF8.GetString(_utf8, insertAt, parent.End - insertAt);
        string tail = gap.Contains('\n', StringComparison.Ordinal) ? gap : _newLine + closeIndent;
        Replace(insertAt, parent.End, memberText + tail);

        if (parent.Members.Count > 0)
        {
            MemberSpan last = parent.Members[^1];
            if (FindComma(last.ValueEnd, insertAt) < 0)
            {
                Replace(last.ValueEnd, last.ValueEnd, ",");
            }
        }
    }

    private string Serialize(JsonNode? value, string indent)
    {
        var options = new JsonWriterOptions
        {
            Indented = true,
            IndentCharacter = _indentUnit[0],
            IndentSize = _indentUnit.Length,
            NewLine = _newLine,

            // Keep values such as Base64 secrets readable ('+' is escaped by the default encoder)
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer);
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace(_newLine, _newLine + indent, StringComparison.Ordinal);
    }

    private int RootStart()
    {
        var reader = new Utf8JsonReader(_utf8, ReaderOptions);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return (int)reader.TokenStartIndex;
            }
        }

        throw new JsonException("The root of the document must be an object.");
    }

    private ObjectSpan ReadObject(int start)
    {
        var reader = new Utf8JsonReader(_utf8.AsSpan(start), ReaderOptions);
        reader.Read();

        var members = new List<MemberSpan>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Comment:
                    continue;

                case JsonTokenType.EndObject:
                    return new ObjectSpan(start, start + (int)reader.TokenStartIndex, members);

                case JsonTokenType.PropertyName:
                    string name = reader.GetString()!;
                    int nameStart = start + (int)reader.TokenStartIndex;
                    do
                    {
                        reader.Read();
                    }
                    while (reader.TokenType == JsonTokenType.Comment);

                    int valueStart = start + (int)reader.TokenStartIndex;
                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    {
                        reader.Skip();
                    }

                    members.Add(new MemberSpan(name, nameStart, valueStart, start + (int)reader.BytesConsumed));
                    break;
            }
        }

        throw new JsonException("Unterminated object.");
    }

    private void Replace(int start, int end, string text)
    {
        byte[] replacement = Encoding.UTF8.GetBytes(text);
        _utf8 = [.. _utf8.AsSpan(0, start), .. replacement, .. _utf8.AsSpan(end)];
    }

    /// <summary>
    /// Finds the comma between two positions, skipping whitespace and comments (returns -1 if there is none).
    /// </summary>
    private int FindComma(int start, int end)
    {
        int index = start;
        while (index < end)
        {
            byte current = _utf8[index];
            if (current == ',')
            {
                return index;
            }

            if (current == '/' && index + 1 < end && _utf8[index + 1] == '/')
            {
                while (index < end && _utf8[index] != '\n')
                {
                    index++;
                }
            }
            else if (current == '/' && index + 1 < end && _utf8[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < end && (_utf8[index] != '*' || _utf8[index + 1] != '/'))
                {
                    index++;
                }

                index += 2;
            }
            else if (IsWhitespaceByte(current))
            {
                index++;
            }
            else
            {
                return -1;
            }
        }

        return -1;
    }

    private int LineStart(int position)
    {
        int index = position;
        while (index > 0 && _utf8[index - 1] != '\n')
        {
            index--;
        }

        return index;
    }

    private string LineIndentation(int position)
    {
        int start = LineStart(position);
        int end = start;
        while (end < position && (_utf8[end] == ' ' || _utf8[end] == '\t'))
        {
            end++;
        }

        return Encoding.UTF8.GetString(_utf8, start, end - start);
    }

    private int SkipSpaces(int position)
    {
        int index = position;
        while (index < _utf8.Length && (_utf8[index] == ' ' || _utf8[index] == '\t'))
        {
            index++;
        }

        return index;
    }

    private bool StartsNewLine(int position)
        => position < _utf8.Length && (_utf8[position] == '\n' || (_utf8[position] == '\r' && position + 1 < _utf8.Length && _utf8[position + 1] == '\n'));

    private bool IsWhitespace(int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            if (_utf8[index] != ' ' && _utf8[index] != '\t')
            {
                return false;
            }
        }

        return true;
    }

    private bool IsWhitespaceOnly(int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            if (IsWhitespaceByte(_utf8[index]) is false)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWhitespaceByte(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private sealed record MemberSpan(string Name, int NameStart, int ValueStart, int ValueEnd);

    private sealed record ObjectSpan(int Start, int End, List<MemberSpan> Members)
    {
        public int IndexOf(string name) => Members.FindLastIndex(member => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase));

        public MemberSpan? Find(string name)
        {
            int index = IndexOf(name);
            return index < 0 ? null : Members[index];
        }
    }
}

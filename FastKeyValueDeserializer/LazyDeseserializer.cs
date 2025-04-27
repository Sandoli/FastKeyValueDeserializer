using System.Text;

namespace FastKeyValueDeserializer;

public static class LazyDeserializer
{
    public static LazyDict DecodeFile(string filePath)
    {
        var builder = new StringBuilder();
        var commentSep = "--".AsSpan();
        using var reader = new StreamReader(filePath);

        while (reader.ReadLine() is { } line)
        {
            var span = line.AsSpan().Trim();

            if (span.IsEmpty || span.StartsWith(commentSep))
                continue;

            var commentIdx = span.IndexOfSeparator(commentSep);

            if (commentIdx >= 0)
                span = span[..commentIdx].Trim();

            if (span.IsEmpty)
                continue;

            var endsWithSemicolon = span.EndsWith(';');

            if (endsWithSemicolon)
                span = span[..^1].TrimEnd();

            builder.Append(span);

            builder.Append(endsWithSemicolon ? '\n' : ' ');
        }

        var buffer = builder.ToString().AsMemory();
        return new LazyDict(DecodeBuffer(buffer));
    }

    internal static Dictionary<string, object> DecodeBuffer(ReadOnlyMemory<char> buffer)
    {
        var result = new Dictionary<string, object>();
        var span = buffer.Span;

        var pos = 0;
        var length = span.Length;

        while (pos < length)
        {
            var lineStart = pos;
            while (pos < length && span[pos] != '\n' && span[pos] != '\r') pos++;
            var line = span.Slice(lineStart, pos - lineStart).Trim();

            var sepIdx = line.IndexOf(":=".AsSpan());
            //var sepIdx = line.IndexOfSeparator(":=".AsSpan());

            if (sepIdx >= 0)
            {
                var key = line[..sepIdx].FastTrim();
                var val = line[(sepIdx + 2)..].FastTrim(out var valOffsetInLine, out _);

                var absValStart = lineStart + sepIdx + 2 + valOffsetInLine;
                SetValue(result, key, buffer.Slice(absValStart, val.Length));
            }

            SkipLineEnd(span, ref pos);
        }

        return result;
    }

    private static void SkipLineEnd(ReadOnlySpan<char> span, ref int pos)
    {
        if (pos < span.Length && span[pos] == '\n') pos++;
    }

    private static void SetValue(Dictionary<string, object> dict, ReadOnlySpan<char> key, ReadOnlyMemory<char> value)
    {
        Span<Range> parts = stackalloc Range[16];
        var count = key.Split(parts, '.');

        var current = dict;
        for (var i = 0; i < count - 1; i++)
        {
            var part = key[parts[i]].ToString();
            current = GetOrCreateSubDictionary(part, current);
        }

        var lastKey = key[parts[count - 1]].ToString();
        current[lastKey] = value;
    }

    private static Dictionary<string, object> GetOrCreateSubDictionary(string part, Dictionary<string, object> current)
    {
        if (!current.TryGetValue(part, out var node) || node is not Dictionary<string, object> subDict)
        {
            subDict = new Dictionary<string, object>();
            current[part] = subDict;
        }

        current = subDict;
        return current;
    }
}
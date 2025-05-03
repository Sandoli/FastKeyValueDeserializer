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
            var span = line.AsSpan();

            // Ignore les lignes vides
            int start = 0;
            while (start < span.Length && char.IsWhiteSpace(span[start])) start++;
            if (start == span.Length)
                continue;

            span = span.Slice(start);

            // Ignore les lignes de commentaires
            if (span.StartsWith(commentSep))
                continue;

            // Supprime le commentaire en ligne
            int commentIdx = span.IndexOfSeparator(commentSep);
            if (commentIdx >= 0)
            {
                span = span.Slice(0, commentIdx);

                // Trim fin
                int end = span.Length - 1;
                while (end >= 0 && char.IsWhiteSpace(span[end])) end--;
                span = span.Slice(0, end + 1);

                if (span.IsEmpty)
                    continue;
            }

            // Supprime le point-virgule de fin s’il est là
            bool endsWithSemicolon = span.Length > 0 && span[^1] == ';';
            if (endsWithSemicolon)
            {
                int end = span.Length - 2;
                while (end >= 0 && char.IsWhiteSpace(span[end])) end--;
                span = span.Slice(0, end + 1);
            }

            builder.Append(span);
            builder.Append(endsWithSemicolon ? '\n' : ' ');
        }
        
        var buffer = builder.ToString().AsMemory();
        return new LazyDict(DecodeBuffer(buffer));
    }

    internal static Dictionary<string, object> DecodeBuffer(ReadOnlyMemory<char> buffer)
    {
        // var result = new Dictionary<string, object>();
        // var span = buffer.Span;
        //
        // var pos = 0;
        // var length = span.Length;
        //
        // while (pos < length)
        // {
        //     var lineStart = pos;
        //     while (pos < length && span[pos] != '\n' && span[pos] != '\r') pos++;
        //     var line = span.Slice(lineStart, pos - lineStart).Trim();
        //
        //     var sepIdx = line.IndexOf(":=".AsSpan());
        //     //var sepIdx = line.IndexOfSeparator(":=".AsSpan());
        //
        //     if (sepIdx >= 0)
        //     {
        //         var key = line[..sepIdx].FastTrim();
        //         var val = line[(sepIdx + 2)..].FastTrim(out var valOffsetInLine, out _);
        //
        //         var absValStart = lineStart + sepIdx + 2 + valOffsetInLine;
        //         SetValue(result, key, buffer.Slice(absValStart, val.Length));
        //     }
        //
        //     SkipLineEnd(span, ref pos);
        // }
        //
        // return result;
        
        var result = new Dictionary<string, object>();
        var span = buffer.Span;
        int pos = 0;
        int length = span.Length;

        while (pos < length)
        {
            int lineStart = pos;

            // Cherche fin de ligne
            while (pos < length && span[pos] != '\n' && span[pos] != '\r') pos++;
            int lineEnd = pos;

            // Avance au début de la prochaine ligne
            if (pos < length && span[pos] == '\r') pos++;
            if (pos < length && span[pos] == '\n') pos++;

            if (lineEnd <= lineStart)
                continue;

            var line = span.Slice(lineStart, lineEnd - lineStart);

            // Recherche du séparateur := dans la ligne
            int sepIdx = line.IndexOf(":=".AsSpan());
            if (sepIdx < 0)
                continue;

            // Détection des offsets exacts de key et value sans Trim
            int keyStart = 0;
            int keyEnd = sepIdx;
            while (keyEnd > keyStart && char.IsWhiteSpace(line[keyEnd - 1])) keyEnd--;

            int valStart = sepIdx + 2;
            while (valStart < line.Length && char.IsWhiteSpace(line[valStart])) valStart++;

            var key = line.Slice(keyStart, keyEnd - keyStart);
            var value = buffer.Slice(lineStart + valStart, lineEnd - (lineStart + valStart));

            SetValue(result, key, value);
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
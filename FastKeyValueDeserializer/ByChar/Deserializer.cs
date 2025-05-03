using System.Globalization;
using System.Text;

namespace FastKeyValueDeserializer.ByChar;

public static class AdvancedKeyValueDeserializer
{
    public static LazyDict2 DecodeFile(string filePath)
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

            span = span[start..];

            // Ignore les lignes de commentaires
            if (span.StartsWith(commentSep))
                continue;

            // Supprime le commentaire en ligne
            int commentIdx = span.IndexOfSeparator(commentSep);
            if (commentIdx >= 0)
            {
                span = span[..commentIdx];

                // Trim fin
                int end = span.Length - 1;
                while (end >= 0 && char.IsWhiteSpace(span[end])) end--;
                span = span[..(end + 1)];

                if (span.IsEmpty)
                    continue;
            }

            // Supprime le point-virgule de fin s’il est là
            bool endsWithSemicolon = span.Length > 0 && span[^1] == ';';
            if (endsWithSemicolon)
            {
                int end = span.Length - 2;
                while (end >= 0 && char.IsWhiteSpace(span[end])) end--;
                span = span[..(end + 1)];
            }

            builder.Append(span);
            builder.Append(endsWithSemicolon ? '\n' : ' ');
        }

        var buffer = builder.ToString().AsMemory();
        return DecodeBuffer(buffer);
    }

    private static LazyDict2 DecodeBuffer(ReadOnlyMemory<char> buffer)
    {
        var result = new Dictionary<string, object>();
        var state = ParserState.Initial;
        var keyBuilder = new StringBuilder();
        string? currentKey = null;
        bool inQuotes = false;
        bool insideList = false;
        bool keyHasEnded = false;
        int i = 0;
        int? valueStart = null;

        while (i < buffer.Length)
        {
            char c = buffer.Span[i];

            switch (state)
            {
                case ParserState.Initial:
                    if (char.IsWhiteSpace(c))
                    {
                        i++;
                        continue;
                    }

                    state = ParserState.Key;
                    //if (!char.IsWhiteSpace(c)) keyBuilder.Append(c);
                    break;

                case ParserState.Key:
                    if (c == ':' && i + 1 < buffer.Length && buffer.Span[i + 1] == '=')
                    {
                        i += 2;
                        currentKey = keyBuilder.ToString();
                        keyBuilder.Clear();
                        state = ParserState.Value;
                        continue;
                    }

                    if (char.IsWhiteSpace(c))
                    {
                        keyHasEnded = true;
                    }
                    else if (!keyHasEnded)
                    {
                        keyBuilder.Append(c);
                    }

                    i++;
                    break;

                case ParserState.Value:
                    if (valueStart == null) valueStart = i;

                    if (c == '"') inQuotes = !inQuotes;
                    else if (c == '(' && !inQuotes) insideList = true;
                    else if (c == ')' && !inQuotes) insideList = false;
                    else if (c == '\n' && !inQuotes && !insideList)
                    {
                        if (currentKey != null)
                        {
                            int valueEnd = i;
                            result[currentKey] = buffer.Slice(valueStart.Value, valueEnd - valueStart.Value);
                        }

                        valueStart = null;
                        state = ParserState.Initial;
                        currentKey = null;
                        keyHasEnded = false;
                        i++;
                        continue;
                    }

                    i++;
                    break;
            }
        }

        return new LazyDict2(result);
    }

    enum ParserState
    {
        Initial,
        Key,
        Value
    }

    private static void SetValue(Dictionary<string, object> dict, string composedKey, object value)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        foreach (char c in composedKey)
        {
            if (c == '.')
            {
                parts.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        parts.Add(sb.ToString());

        var current = dict;
        for (int i = 0; i < parts.Count - 1; i++)
        {
            if (!current.TryGetValue(parts[i], out var node) || node is not Dictionary<string, object> subDict)
            {
                subDict = new Dictionary<string, object>();
                current[parts[i]] = subDict;
            }

            current = subDict;
        }

        current[parts[^1]] = value;
    }

    internal static object ParseValue(ReadOnlySpan<char> value)
    {
        int i = 0;
        while (i < value.Length && char.IsWhiteSpace(value[i])) i++;
        while (value.Length > 0 && char.IsWhiteSpace(value[^1])) value = value[..^1];

        if (i < value.Length && value[i] == '(' && value[^1] == ')')
        {
            var list = new List<object>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int j = i + 1; j < value.Length - 1; j++)
            {
                char c = value[j];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    list.Add(ParseValue(sb.ToString()));
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
                list.Add(ParseValue(sb.ToString()));
            return list;
        }

        if (value.StartsWith("\"") && value.EndsWith("\""))
            return value.Slice(1, value.Length - 2).ToString();

        if (int.TryParse(value, out var intVal)) return intVal;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblVal)) return dblVal;

        return value.Trim().ToString();
    }
}

public class LazyDict2 : IDict
{
    private readonly Dictionary<string, object> _dict;

    internal LazyDict2(Dictionary<string, object> dict)
    {
        _dict = dict;
    }

    public object this[string key] => GetValue(key);

    public object GetValue(string key)
    {
        if (TryGetValue(key, out var val))
            return val;
        throw new KeyNotFoundException(key);
    }

    public T Get<T>(string key)
    {
        if (TryGet<T>(key, out var val))
            return val;
        throw new KeyNotFoundException(key);
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (TryGetValue(key, out var obj))
        {
            if (obj is T t)
            {
                value = t;
                return true;
            }

            if (typeof(T) == typeof(double) && obj is int i)
            {
                value = (T)(object)(double)i;
                return true;
            }

            if (typeof(T) == typeof(string) && obj is ReadOnlyMemory<char> mem)
            {
                value = (T)(object)mem.ToString();
                return true;
            }
        }

        value = default!;
        return false;
    }

    private bool TryGetValue(string key, out object value)
    {
        var parts = key.Split('.');
        var current = _dict;
        value = string.Empty;

        for (var i = 0; i < parts.Length; i++)
        {
            if (!current.TryGetValue(parts[i], out var node))
                return false;

            if (i == parts.Length - 1)
            {
                if (node is ReadOnlyMemory<char> lazy)
                {
                    value = AdvancedKeyValueDeserializer.ParseValue(lazy.Span);
                    current[parts[i]] = value;
                    return true;
                }

                value = node;
                return true;
            }

            if (node is Dictionary<string, object> next)
                current = next;
            else
                return false;
        }

        return false;
    }
}
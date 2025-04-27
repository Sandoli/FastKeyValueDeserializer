using System.Text;

namespace FastKeyValueDeserializer;

public class FastDict : IDict
{
    private readonly Dictionary<string, object> _dict;

    internal FastDict(Dictionary<string, object> dict)
    {
        _dict = dict;
    }

    public T Get<T>(string composedKey)
    {
        if (TryGet<T>(composedKey, out var val))
            return val;
        throw new KeyNotFoundException(composedKey);
    }

    public bool TryGet<T>(string composedKey, out T value)
    {
        if (TryGetValue(composedKey, out var obj))
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

    public object GetValue(string composedKey)
    {
        if (TryGetValue(composedKey, out var val))
            return val;
        throw new KeyNotFoundException(composedKey);
    }

    public object this[string key] => GetValue(key);

    private bool TryGetValue(string composedKey, out object value)
    {
        var parts = composedKey.Split('.');
        var current = _dict;
        value = string.Empty;

        for (var i = 0; i < parts.Length; i++)
        {
            if (!current.TryGetValue(parts[i], out var node))
                return false;

            if (i == parts.Length - 1)
            {
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

public static class FastDeserializer
{
    public static IDict DecodeFile(string filePath)
    {
        const int bufferSize = 2048;
        using var fileStream = File.OpenRead(filePath);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, true, bufferSize);
        return new FastDict(DecodeLines(ReadLines(reader)));
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static Dictionary<string, object> DecodeLines(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, object>();
        var commentSep = "--".AsSpan();
        var sb = new StringBuilder();
        var currentKey = ReadOnlySpan<char>.Empty;

        foreach (var rawLine in lines)
        {
            var item = rawLine.AsSpan().Trim();
            if (item.IsEmpty || item.StartsWith(commentSep)) continue;

            var commentIdx = item.IndexOfSeparator(commentSep);
            if (commentIdx >= 0)
                item = item.Slice(0, commentIdx).Trim();

            if (item.IsEmpty) continue;

            if (item.EndsWith(';'))
            {
                item = item[..^1];
                if (!currentKey.IsEmpty)
                {
                    sb.Append(item);
                    var fullValue = sb.ToString();
                    SetValue(result, currentKey, fullValue.AsSpan());
                    sb.Clear();
                    currentKey = ReadOnlySpan<char>.Empty;
                    continue;
                }

                var sepIdx = item.IndexOf(":=".AsSpan());
                if (sepIdx >= 0)
                {
                    var key = item[..sepIdx].Trim();
                    var val = item[(sepIdx + 2)..].Trim();
                    SetValue(result, key, val);
                }
            }
            else
            {
                var sepIdx = item.IndexOf(":=".AsSpan());
                if (sepIdx >= 0)
                {
                    currentKey = item[..sepIdx].Trim();
                    var val = item[(sepIdx + 2)..].Trim();
                    sb.Append(val);
                }
                else
                {
                    sb.Append(item);
                }
            }
        }

        return result;
    }

    private static void SetValue(Dictionary<string, object> dict, ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        Span<Range> parts = stackalloc Range[16];
        var count = key.Split(parts, '.');

        var current = dict;
        for (var i = 0; i < count - 1; i++)
        {
            var part = key[parts[i]].ToString();
            if (!current.TryGetValue(part, out var node) || node is not Dictionary<string, object> subDict)
            {
                subDict = new Dictionary<string, object>();
                current[part] = subDict;
            }

            current = subDict;
        }

        var lastKey = key[parts[count - 1]].ToString();
        current[lastKey] = Utils.ParseValue(value);
    }
}
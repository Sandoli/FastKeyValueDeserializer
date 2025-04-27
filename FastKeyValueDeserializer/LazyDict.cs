namespace FastKeyValueDeserializer;

public class LazyDict : IDict
{
    private readonly Dictionary<string, object> _dict;

    internal LazyDict(Dictionary<string, object> dict)
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
                    value = Utils.ParseValue(lazy.Span);
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
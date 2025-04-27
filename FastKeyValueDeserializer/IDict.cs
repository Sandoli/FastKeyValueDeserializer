namespace FastKeyValueDeserializer;

public interface IDict
{
    object this[string key] { get; }
    T Get<T>(string key);
    bool TryGet<T>(string key, out T value);
    object GetValue(string key);
}
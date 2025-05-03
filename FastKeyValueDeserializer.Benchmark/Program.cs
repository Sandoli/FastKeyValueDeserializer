// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastKeyValueDeserializer;

internal class Program
{
    public static void Main(string[] _)
    {
        BenchmarkRunner.Run<BenchmarkSerializer>();
    }
}

[MemoryDiagnoser]
public class BenchmarkSerializer
{
    [Benchmark]
    public void DeserializeFull()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = FastDeserializer.DecodeFile(filePath);

        var objects = result.GetValue("key5.subkey5.2") as List<object>;
    }

    [Benchmark]
    public void DeserializeLazy()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = LazyDeserializer.DecodeFile(filePath);

        var objects = result.GetValue("key5.subkey5.2") as List<object>;
    }

    [Benchmark]
    public void DeserializeByChar()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result= FastKeyValueDeserializer.ByChar.AdvancedKeyValueDeserializer.DecodeFile(filePath);

        var objects = result.GetValue("key5.subkey5.2") as List<object>;
    }
}
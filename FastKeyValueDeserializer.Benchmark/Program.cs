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
    public void SerializeFull()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = FastDeserializer.DecodeFile(filePath);

        var objects = result.GetValue("key5.subkey5.2") as List<object>;
    }

    [Benchmark]
    public void SerializeLazy()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = LazyDeserializer.DecodeFile(filePath);

        var objects = result.GetValue("key5.subkey5.2") as List<object>;
    }

    [Benchmark]
    public async Task SerializeParallelLazy()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = await ParallelLazyDeserializer.DecodeFile(filePath);

        var objects = result.GetValue("key5.subkey5.2") as List<object>;
    }
}
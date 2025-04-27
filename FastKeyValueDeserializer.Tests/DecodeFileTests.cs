using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FastKeyValueDeserializer.Tests;

public class DecodeFileTests
{
    [Fact]
    public void FastDecodeFile_ShouldParseFileCorrectly()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = FastDeserializer.DecodeFile(filePath);

        // Assert
        Assert.NotNull(result);
        var val1 = result.Get<string>("key1");
        Assert.Equal("value1", val1);
        var val2 = result.GetValue("key2");
        Assert.IsType<Dictionary<string, object>>(val2);
        var subKey2 = result.Get<int>("key2.subkey1");
        Assert.Equal(42, subKey2);
        Assert.Equal("Hello, World!", result.Get<string>("key2.subkey2"));
        Assert.IsType<List<object>>(result.GetValue("key3"));
        var list = (List<object>)result.GetValue("key3");
        Assert.Equal(new object[] { 1, 2, 3, "four", 5.6 }, list);
        Assert.Equal(new object[] { 1, 2, 3, 4, 5.6 }, result.GetValue("key4"));
        Assert.IsType<Dictionary<string, object>>(result["key5"]);
        Assert.Equal("valeur profonde", result.Get<string>("key5.subkey5.1"));
        var objects = result.GetValue("key5.subkey5.2") as List<object>;
        Assert.Equal(173, objects?.Count);
    }

    [Fact]
    public void LazyDecodeFile_ShouldParseFileCorrectly()
    {
        // Arrange
        var filePath = Path.Combine("TestFiles", "testfile.kv");

        // Act
        var result = LazyDeserializer.DecodeFile(filePath);

        // Assert
        Assert.NotNull(result);
        var val1 = result.Get<string>("key1");
        Assert.Equal("value1", val1);
        var val2 = result.GetValue("key2");
        Assert.IsType<Dictionary<string, object>>(val2);
        var subKey2 = result.Get<int>("key2.subkey1");
        Assert.Equal(42, subKey2);
        Assert.Equal("Hello, World!", result.Get<string>("key2.subkey2"));
        Assert.IsType<List<object>>(result.GetValue("key3"));
        var list = (List<object>)result.GetValue("key3");
        Assert.Equal(new object[] { 1, 2, 3, "four", 5.6 }, list);
        Assert.Equal(new object[] { 1, 2, 3, 4, 5.6 }, result.GetValue("key4"));
        Assert.IsType<Dictionary<string, object>>(result["key5"]);
        Assert.Equal("valeur profonde", result.Get<string>("key5.subkey5.1"));
        var objects = result.GetValue("key5.subkey5.2") as List<object>;
        Assert.Equal(173, objects?.Count);
    }
}
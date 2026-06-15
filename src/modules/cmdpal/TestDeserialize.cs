using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// Mimic the JsonRpcResponse
public sealed class TestJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }
    [JsonPropertyName("error")]
    public object? Error { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TestJsonRpcResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
internal partial class TestContext : JsonSerializerContext { }

var json = """{"jsonrpc":"2.0","id":3,"result":{"items":[{"title":"Say Hello","displayName":"Say Hello","subtitle":"Displays a toast message","icon":null,"section":"Commands","tags":[{"text":"Action"}],"command":{"id":"say-hello","name":"Say Hello","displayName":"Say Hello"}},{"title":"View Readme","displayName":"View Readme","subtitle":"Shows a markdown content page","icon":null,"section":"Pages","tags":[{"text":"Page"}],"command":{"id":"open-markdown","name":"View Readme","displayName":"View Readme"}},{"title":"Static Item","displayName":"Static Item","subtitle":"This item does not have an action","icon":null,"section":"Other","command":{"id":"item-3","name":"Static Item","displayName":"Static Item"}}]}}""";

Console.WriteLine($"Input JSON length: {json.Length}");

var response = JsonSerializer.Deserialize(json, TestContext.Default.TestJsonRpcResponse);
Console.WriteLine($"Response is null: {response == null}");
Console.WriteLine($"Response.Id: {response?.Id}");
Console.WriteLine($"Response.Result.HasValue: {response?.Result.HasValue}");
Console.WriteLine($"Response.Result.Value.ValueKind: {response?.Result?.ValueKind}");

if (response?.Result?.ValueKind == JsonValueKind.Object)
{
    var result = response.Result.Value;
    if (result.TryGetProperty("items", out var itemsProp))
    {
        Console.WriteLine($"items.ValueKind: {itemsProp.ValueKind}");
        if (itemsProp.ValueKind == JsonValueKind.Array)
        {
            Console.WriteLine($"items count: {itemsProp.GetArrayLength()}");
            foreach (var item in itemsProp.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var t))
                    Console.WriteLine($"  item title: {t.GetString()}");
            }
        }
    }
    else
    {
        Console.WriteLine("ERROR: 'items' property not found!");
        Console.WriteLine($"Available properties:");
        foreach (var prop in result.EnumerateObject())
        {
            Console.WriteLine($"  {prop.Name}: {prop.Value.ValueKind}");
        }
    }
}
else
{
    Console.WriteLine($"ERROR: Result is not an object! ValueKind = {response?.Result?.ValueKind}");
}

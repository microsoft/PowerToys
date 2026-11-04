// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
public sealed class ExplorerScriptServiceTests
{
    private static readonly string[] ExpectedCalls = ["list:file-explorer", "list:file-explorer", "transform:file-explorer"];

    [TestMethod]
    public async Task DiscoveryAndTransformAreAttributedAndUseTypedParametersAndCurrentMetadata()
    {
        using var fixture = new Fixture();
        fixture.Write("catalogue.json", Catalogue(output: "text", parameterType: "int"));
        fixture.Write("result.json", """{"text":"produced"}""");
        var service = Service(fixture);
        var scripts = await service.GetApplicableScriptsAsync([fixture.Input], CancellationToken.None);
        Assert.AreEqual(1, scripts.Count);
        var stale = scripts[0];
        fixture.Write("catalogue.json", Catalogue(output: "text", parameterType: "int", name: "Current name"));
        var parameters = new Dictionary<string, JsonElement> { ["amount"] = JsonSerializer.SerializeToElement(2) };
        var outputs = await service.ExecuteAsync(stale, [fixture.Input], parameters, fixture.Destination, CancellationToken.None);
        Assert.AreEqual("sample - Current_name.txt", Path.GetFileName(outputs.Single()));
        using var payload = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "payload.json")));
        Assert.AreEqual(fixture.Input, payload.RootElement.GetProperty("files")[0].GetString());
        Assert.AreEqual(2, payload.RootElement.GetProperty("parameters").GetProperty("amount").GetInt32());
        CollectionAssert.AreEqual(ExpectedCalls, await File.ReadAllLinesAsync(Path.Combine(fixture.Root, "calls.txt")));
        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
    }

    [TestMethod]
    public async Task RemovedScriptOrChangedSelectionContractNeverInvokesTransform()
    {
        using var fixture = new Fixture();
        fixture.Write("catalogue.json", Catalogue("text", "int"));
        var service = Service(fixture);
        var stale = (await service.GetApplicableScriptsAsync([fixture.Input]))[0];
        foreach (var catalogue in new[] { "[]", Catalogue("text", "int", extension: ".txt") })
        {
            fixture.Write("catalogue.json", catalogue);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement>(), fixture.Destination));
        }

        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "payload.json")));
    }

    [TestMethod]
    public async Task ChangedParameterTypeBoundsAndRequiredFieldsAreRevalidatedBeforeTransform()
    {
        using var fixture = new Fixture();
        fixture.Write("catalogue.json", Catalogue("text", "int"));
        var service = Service(fixture);
        var stale = (await service.GetApplicableScriptsAsync([fixture.Input]))[0];
        fixture.Write("catalogue.json", Catalogue("text", "bool"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement> { ["amount"] = JsonSerializer.SerializeToElement(2) }, fixture.Destination));
        fixture.Write("catalogue.json", Catalogue("text", "int"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement> { ["amount"] = JsonSerializer.SerializeToElement("2") }, fixture.Destination));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement> { ["amount"] = JsonSerializer.SerializeToElement(100) }, fixture.Destination));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement>(), fixture.Destination));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement> { ["unknown"] = JsonSerializer.SerializeToElement(2) }, fixture.Destination));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "payload.json")));
    }

    [TestMethod]
    public async Task ScriptWithoutParameterBagOmitsParametersFromActualHostInput()
    {
        using var fixture = new Fixture();
        const string catalogue = """
            [{"id":"script","name":"No parameters","io":{"input":"files","output":"text"},
              "inputSchema":{"type":"object","properties":{"files":{"type":"array","items":{"type":"string"}}},"required":["files"],"additionalProperties":false}}]
            """;
        fixture.Write("catalogue.json", catalogue);
        fixture.Write("result.json", """{"text":"produced"}""");
        var service = Service(fixture);
        var script = (await service.GetApplicableScriptsAsync([fixture.Input])).Single();
        var paths = await service.ExecuteAsync(script, [fixture.Input], ScriptParameters.GetDefaults(script), fixture.Destination);
        using var payload = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "payload.json")));
        Assert.AreEqual(1, payload.RootElement.EnumerateObject().Count());
        Assert.AreEqual(fixture.Input, payload.RootElement.GetProperty("files")[0].GetString());
        Assert.IsFalse(payload.RootElement.TryGetProperty("parameters", out _));
        Assert.AreEqual("produced", await File.ReadAllTextAsync(paths.Single()));
    }

    [TestMethod]
    public async Task RefreshedDeclaredEmptyBagIsPreservedInActualHostInput()
    {
        using var fixture = new Fixture();
        const string initialCatalogue = """
            [{"id":"script","io":{"input":"files","output":"text"},
              "inputSchema":{"type":"object","properties":{"files":{"type":"array"}}}}]
            """;
        fixture.Write("catalogue.json", initialCatalogue);
        fixture.Write("result.json", """{"text":"produced"}""");
        var service = Service(fixture);
        var stale = (await service.GetApplicableScriptsAsync([fixture.Input])).Single();
        const string currentCatalogue = """
            [{"id":"script","io":{"input":"files","output":"text"},
              "inputSchema":{"type":"object","properties":{"files":{"type":"array"},"parameters":{"type":"object","properties":{},"additionalProperties":false}},"required":["files","parameters"]}}]
            """;
        fixture.Write("catalogue.json", currentCatalogue);
        await service.ExecuteAsync(stale, [fixture.Input], new Dictionary<string, JsonElement>(), fixture.Destination);
        using var payload = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "payload.json")));
        var bag = payload.RootElement.GetProperty("parameters");
        Assert.AreEqual(JsonValueKind.Object, bag.ValueKind);
        Assert.AreEqual(0, bag.EnumerateObject().Count());
    }

    [TestMethod]
    public async Task DeclaredOptionalFileBagRemainsEmptyWhenUnsetAndAppliesValidDefaults()
    {
        using var fixture = new Fixture();
        fixture.Write("catalogue.json", OptionalFileCatalogue(defaultValue: null));
        fixture.Write("result.json", """{"text":"produced"}""");
        var service = Service(fixture);
        var script = (await service.GetApplicableScriptsAsync([fixture.Input])).Single();
        await service.ExecuteAsync(script, [fixture.Input], new Dictionary<string, JsonElement>(), fixture.Destination);
        using (var payload = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "payload.json"))))
        {
            Assert.AreEqual(0, payload.RootElement.GetProperty("parameters").EnumerateObject().Count());
        }

        fixture.Write("catalogue.json", OptionalFileCatalogue(fixture.Input));
        await service.ExecuteAsync(script, [fixture.Input], new Dictionary<string, JsonElement>(), fixture.Destination);
        using (var payload = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "payload.json"))))
        {
            Assert.AreEqual(fixture.Input, payload.RootElement.GetProperty("parameters").GetProperty("reference").GetString());
        }

        File.Delete(Path.Combine(fixture.Root, "payload.json"));
        fixture.Write("catalogue.json", OptionalFileCatalogue(fixture.Destination));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(script, [fixture.Input], new Dictionary<string, JsonElement>(), fixture.Destination));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "payload.json")));
    }

    [TestMethod]
    public async Task RefreshedSchemaWithoutBagDoesNotSilentlyDiscardDeclaredParameterValues()
    {
        using var fixture = new Fixture();
        fixture.Write("catalogue.json", Catalogue("text", "int"));
        var service = Service(fixture);
        var script = (await service.GetApplicableScriptsAsync([fixture.Input])).Single();
        const string catalogue = """
            [{"id":"script","io":{"input":"files","output":"text"},
              "inputSchema":{"type":"object","properties":{"files":{"type":"array"}}},
              "parameters":[{"name":"amount","type":"int","isRequired":true}]}]
            """;
        fixture.Write("catalogue.json", catalogue);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(
            script,
            [fixture.Input],
            new Dictionary<string, JsonElement> { ["amount"] = JsonSerializer.SerializeToElement(2) },
            fixture.Destination));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "payload.json")));
    }

    [TestMethod]
    public async Task CancelledDiscoveryDoesNotStartHost()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Service(fixture).GetApplicableScriptsAsync([fixture.Input], cancellation.Token));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "calls.txt")));
    }

    private static ExplorerScriptService Service(Fixture fixture) =>
        new(new PowerScriptsClient(ExplorerScriptService.ConsumerId, Path.Combine(AppContext.BaseDirectory, "fake-host", "FakeHost.exe"), fixture.Root));

    private static string OptionalFileCatalogue(string? defaultValue) =>
        """
        [{"id":"script","io":{"input":"files","output":"text"},
          "inputSchema":{"type":"object","properties":{"files":{"type":"array"},"parameters":{"type":"object","properties":{"reference":{"type":"string"}}}}},
          "parameters":[{"name":"reference","label":"Reference file","type":"file","isRequired":false,"default":DEFAULT_VALUE}]}]
        """.Replace("DEFAULT_VALUE", JsonSerializer.Serialize(defaultValue), StringComparison.Ordinal);

    private static string Catalogue(string output, string parameterType, string name = "Old name", string extension = ".md") =>
        $$$"""
        [
          {"id":"script","name":"{{{name}}}","io":{"input":"files","output":"{{{output}}}"},"input":{"extensions":["{{{extension}}}"],"minFiles":1,"maxFiles":2},
           "parameters":[{"name":"amount","label":"Amount","type":"{{{parameterType}}}","isRequired":true,"min":1,"max":5}]},
          {"id":"image","io":{"input":"image","output":"image"}},
          {"id":"wrong-extension","io":{"input":"files","output":"none"},"input":{"extensions":[".png"]}}
        ]
        """;
}

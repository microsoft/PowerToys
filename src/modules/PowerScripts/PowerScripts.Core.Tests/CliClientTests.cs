// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliClientTests
{
    private const string CatalogJson = """
    {
      "succeeded": true,
      "commands": [
        {
          "id": "example.run",
          "name": "Run example",
          "group": "Example module",
          "description": "Runs an example command.",
          "isAvailable": true,
          "parameters": [
            {
              "name": "count",
              "type": "int",
              "isRequired": true,
              "label": "Repeat count",
              "description": "Number of repetitions.",
              "default": "2",
              "options": [],
              "min": 1,
              "max": 10
            },
            {
              "name": "mode",
              "type": "choice",
              "isRequired": false,
              "options": ["standard", "quiet"]
            }
          ]
        },
        {
          "id": "example.unavailable",
          "name": "Unavailable example",
          "group": "Example module",
          "description": "",
          "isAvailable": false,
          "unavailableReason": "The module is not installed.",
          "parameters": []
        }
      ]
    }
    """;

    [TestMethod]
    public void CatalogParse_MapsAvailabilityAndTypedParameters()
    {
        var result = CliCatalogResult.Parse(CatalogJson);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Error);
        Assert.AreEqual(2, result.Commands.Count);
        var command = result.Commands[0];
        Assert.AreEqual("example.run", command.Id);
        Assert.AreEqual("Run example", command.Name);
        Assert.AreEqual("Example module", command.Group);
        Assert.AreEqual("Runs an example command.", command.Description);
        Assert.IsTrue(command.IsAvailable);
        Assert.IsNull(command.UnavailableReason);
        Assert.AreEqual(2, command.Parameters.Count);
        var count = command.Parameters[0];
        Assert.AreEqual("count", count.Name);
        Assert.AreEqual("int", count.Type);
        Assert.IsTrue(count.IsRequired);
        Assert.AreEqual("Repeat count", count.Label);
        Assert.AreEqual("Number of repetitions.", count.Description);
        Assert.AreEqual("2", count.Default);
        Assert.AreEqual(1, count.Min);
        Assert.AreEqual(10, count.Max);
        CollectionAssert.AreEqual(new[] { "standard", "quiet" }, command.Parameters[1].Options.ToArray());
        Assert.IsFalse(result.Commands[1].IsAvailable);
        Assert.AreEqual("The module is not installed.", result.Commands[1].UnavailableReason);
    }

    [TestMethod]
    public void CatalogParse_DistinguishesAnEmptyCatalogFromHostFailure()
    {
        var empty = CliCatalogResult.Parse("""{"succeeded":true,"commands":[]}""");
        var failure = CliCatalogResult.Parse("""{"succeeded":false,"error":"PowerScripts is disabled.","commands":[]}""");

        Assert.IsTrue(empty.Succeeded);
        Assert.AreEqual(0, empty.Commands.Count);
        Assert.IsFalse(failure.Succeeded);
        Assert.AreEqual("PowerScripts is disabled.", failure.Error);
    }

    [TestMethod]
    public void CatalogParse_RejectsSuccessEnvelopeFromFailedHost()
    {
        var result = CliCatalogResult.Parse(CatalogJson, hostExitCode: 42);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
        Assert.AreEqual(0, result.Commands.Count);
    }

    [DataTestMethod]
    [DataRow("{\"name\":\"mode\",\"isRequired\":false,\"options\":[]}")]
    [DataRow("{\"name\":\"mode\",\"type\":\"unsupported\",\"isRequired\":false,\"options\":[]}")]
    [DataRow("{\"name\":\"mode\",\"type\":\"string\",\"options\":[]}")]
    [DataRow("{\"name\":\"mode\",\"type\":\"string\",\"isRequired\":\"false\",\"options\":[]}")]
    [DataRow("{\"name\":\"mode\",\"type\":\"string\",\"isRequired\":false}")]
    [DataRow("{\"name\":\"mode\",\"type\":\"choice\",\"isRequired\":false,\"options\":[\"valid\",1]}")]
    public void CatalogParse_RejectsMalformedParameterContracts(string parameterJson)
    {
        var json = "{\"succeeded\":true,\"commands\":[{\"id\":\"example.run\",\"name\":\"Run\",\"group\":\"Example\",\"description\":\"\",\"isAvailable\":true,\"parameters\":[" + parameterJson + "]}]}";
        var result = CliCatalogResult.Parse(json);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("not-json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"succeeded\":true}")]
    [DataRow("{\"succeeded\":true,\"commands\":[null]}")]
    public void CatalogParse_InvalidResponseReturnsExplicitFailure(string json)
    {
        var result = CliCatalogResult.Parse(json);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
        Assert.AreEqual(0, result.Commands.Count);
    }

    [TestMethod]
    public void RunParse_PreservesNonzeroExitCodeAndFutureStatus()
    {
        var result = CliRunResult.Parse("""
        {
          "succeeded": false,
          "status": "futureHostStatus",
          "exitCode": 42,
          "stdOut": "partly completed\n",
          "stdErr": "CLI reported a problem.",
          "error": "Operation failed."
        }
        """);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("futureHostStatus", result.Status);
        Assert.AreEqual(42, result.ExitCode);
        Assert.AreEqual("partly completed\n", result.StdOut);
        Assert.AreEqual("CLI reported a problem.", result.StdErr);
        Assert.AreEqual("Operation failed.", result.Error);
    }

    [TestMethod]
    public void RunParse_ReadsSuccessfulResult()
    {
        var result = CliRunResult.Parse("""{"succeeded":true,"status":"succeeded","exitCode":0,"stdOut":"done","stdErr":""}""");

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("succeeded", result.Status);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("done", result.StdOut);
        Assert.IsNull(result.Error);
    }

    [DataTestMethod]
    [DataRow(42, 0)]
    [DataRow(0, 42)]
    public void RunParse_RejectsSuccessWithNonzeroEnvelopeOrHostExitCode(int envelopeExitCode, int hostExitCode)
    {
        var json = "{\"succeeded\":true,\"status\":\"succeeded\",\"exitCode\":" + envelopeExitCode + ",\"stdOut\":\"\",\"stdErr\":\"\"}";
        var result = CliRunResult.Parse(json, hostExitCode);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("invalidResponse", result.Status);
        Assert.AreEqual(42, result.ExitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("not-json")]
    [DataRow("null")]
    [DataRow("{\"succeeded\":true}")]
    [DataRow("{\"succeeded\":true,\"status\":\"\",\"exitCode\":0,\"stdOut\":\"\",\"stdErr\":\"\"}")]
    public void RunParse_InvalidResponseCannotReportSuccess(string json)
    {
        var result = CliRunResult.Parse(json);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("invalidResponse", result.Status);
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }

    [TestMethod]
    public void BuildCliRunArguments_EncodesSpecialValuesAndUsesStableOrdering()
    {
        const string complexValue = "你好 \"quoted\"\r\n--root C:\\unrelated & $(command) %PATH%";
        var parameters = new Dictionary<string, string?>
        {
            ["z-last"] = null,
            ["a-first"] = complexValue,
        };

        var arguments = PowerScriptsClient.BuildCliRunArguments("example.run", parameters);
        var tokens = arguments.Split(' ');

        Assert.AreEqual(10, tokens.Length);
        CollectionAssert.AreEqual(new[] { "cli", "run", "example.run" }, tokens.Take(3).ToArray());
        Assert.AreEqual("--set-base64", tokens[3]);
        Assert.AreEqual("a-first\0" + complexValue, Encoding.UTF8.GetString(Convert.FromBase64String(tokens[4])));
        Assert.AreEqual("--set-base64", tokens[5]);
        Assert.AreEqual("z-last\0", Encoding.UTF8.GetString(Convert.FromBase64String(tokens[6])));
        Assert.AreEqual("--no-consent", tokens[7]);
        Assert.AreEqual("--timeout-ms", tokens[8]);
        Assert.AreEqual("60000", tokens[9]);
        Assert.IsFalse(tokens.Contains("--root"));
        Assert.IsFalse(tokens.Contains("--json"));
        Assert.AreEqual(arguments, PowerScriptsClient.BuildCliRunArguments("example.run", parameters.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value)));
    }

    [TestMethod]
    public void BuildCliRunArguments_WithNoParametersStillRequiresNoConsent()
    {
        Assert.AreEqual("cli run example.run --no-consent --timeout-ms 60000", PowerScriptsClient.BuildCliRunArguments("example.run", new Dictionary<string, string?>()));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("example.run --root C:\\other")]
    [DataRow("example\" --json")]
    [DataRow("example/run")]
    [DataRow("example\nrun")]
    [DataRow("示例")]
    public void BuildCliRunArguments_RejectsUnsafeCommandId(string id)
    {
        Assert.ThrowsException<ArgumentException>(() => PowerScriptsClient.BuildCliRunArguments(id, new Dictionary<string, string?>()));
    }

    [DataTestMethod]
    [DataRow("", "value")]
    [DataRow("  ", "value")]
    [DataRow("name\0suffix", "value")]
    [DataRow("name", "value\0suffix")]
    public void BuildCliRunArguments_RejectsInvalidParameterPairs(string name, string value)
    {
        Assert.ThrowsException<ArgumentException>(() => PowerScriptsClient.BuildCliRunArguments("example.run", new Dictionary<string, string?> { [name] = value }));
    }

    [TestMethod]
    public void BuildCliRunArguments_RejectsCaseInsensitiveDuplicateNames()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["mode"] = "standard",
            ["Mode"] = "quiet",
        };

        Assert.ThrowsException<ArgumentException>(() => PowerScriptsClient.BuildCliRunArguments("example.run", parameters));
    }

    [TestMethod]
    public async Task ClientWithoutHost_ReturnsExplicitCatalogAndRunFailures()
    {
        var client = CreateUnavailableClient();
        var catalog = await client.GetCliCommandsAsync();
        var run = await client.RunCliAsync("example.run", new Dictionary<string, string?>());

        Assert.IsFalse(catalog.Succeeded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(catalog.Error));
        Assert.AreEqual(0, catalog.Commands.Count);
        Assert.IsFalse(run.Succeeded);
        Assert.AreEqual("unavailable", run.Status);
        Assert.AreNotEqual(0, run.ExitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(run.Error));
    }

    [TestMethod]
    public async Task ClientCancellation_ThrowsEvenWhenHostIsUnavailable()
    {
        var client = CreateUnavailableClient();
        var cancellationToken = new CancellationToken(canceled: true);

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => client.GetCliCommandsAsync(cancellationToken));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => client.RunCliAsync("example.run", new Dictionary<string, string?>(), cancellationToken: cancellationToken));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(120001)]
    public async Task RunCliAsync_RejectsTimeoutOutsideHostRange(int timeoutMs)
    {
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => CreateUnavailableClient().RunCliAsync("example.run", new Dictionary<string, string?>(), timeoutMs));
    }

    [TestMethod]
    public async Task RunCliAsync_ValidatesCommandBeforeHostDiscoveryFailure()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => CreateUnavailableClient().RunCliAsync("example.run --json", new Dictionary<string, string?>()));
    }

    private static PowerScriptsClient CreateUnavailableClient() => new(
        hostPath: Path.Combine(Path.GetTempPath(), "ps-cli-missing-" + Guid.NewGuid().ToString("N"), "PowerScripts.Host.exe"),
        scriptsRoot: Path.Combine(Path.GetTempPath(), "unrelated-script-root"));
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class HostSessionTests
{
    [TestMethod]
    public async Task SessionReloadsDescriptorsAndCannotApproveTrust()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var entry = Path.Combine(root, "sample.ps1");
        File.WriteAllText(entry, "Write-Output 'not executed'");
        var descriptor = entry + ".tool.json";
        File.WriteAllText(descriptor, """{"name":"session_sample","description":"Original","inputSchema":{"type":"object","properties":{},"required":[],"additionalProperties":false},"outputSchema":{"type":"object","properties":{"text":{"type":"string"}},"required":["text"],"additionalProperties":false}}""");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo(PowerScriptsClient.ResolveHostPath() ?? throw new FileNotFoundException("Host missing."))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("session");
        using var host = Process.Start(start) ?? throw new InvalidOperationException("Host did not start.");
        var stderr = host.StandardError.ReadToEndAsync();
        try
        {
            async Task<JsonDocument> Request(params string[] arguments)
            {
                await PowerScriptsSessionProtocol.WriteAsync(host.StandardInput.BaseStream, PowerScriptsSessionProtocol.CreateRequest(arguments, null), timeout.Token);
                return JsonDocument.Parse(await PowerScriptsSessionProtocol.ReadAsync(host.StandardOutput.BaseStream, timeout.Token)
                    ?? throw new EndOfStreamException("Session terminated."));
            }

            async Task<string> Description()
            {
                using var response = await Request("list", "--json", "--include-disabled", "--root", root);
                Assert.AreEqual(0, response.RootElement.GetProperty("exitCode").GetInt32());
                using var scripts = JsonDocument.Parse(response.RootElement.GetProperty("stdout").GetString()!);
                Assert.AreEqual(1, scripts.RootElement.GetArrayLength(), response.RootElement.GetProperty("stderr").GetString());
                return scripts.RootElement.EnumerateArray().Single().GetProperty("description").GetString()!;
            }

            Assert.AreEqual("Original", await Description());
            File.WriteAllText(descriptor, File.ReadAllText(descriptor).Replace("Original", "Updated", StringComparison.Ordinal));
            Assert.AreEqual("Updated", await Description());
            using (var refused = await Request("trust", "approve", "session_sample", "--root", root))
            {
                Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, refused.RootElement.GetProperty("exitCode").GetInt32());
                StringAssert.Contains(refused.RootElement.GetProperty("stderr").GetString(), "--no-consent");
            }

            File.Delete(descriptor);
            using var removed = await Request("list", "--json", "--include-disabled", "--root", root);
            Assert.AreEqual("[]", removed.RootElement.GetProperty("stdout").GetString()!.Trim());
            Assert.IsFalse(host.HasExited, "All requests must use the same live Host.");
            host.StandardInput.Close();
            await host.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(0, host.ExitCode, await stderr);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                host.WaitForExit();
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReusableClientSerializesRequestsAndSurvivesCommandFailure()
    {
        using var client = new PowerScriptsClient("test-harness", reuseHost: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var lists = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.ListAsync(timeout.Token)));
        CollectionAssert.AreEqual(lists[0].Select(script => script.Id).ToArray(), lists[1].Select(script => script.Id).ToArray());
        var result = await client.TransformAsync("missing-" + Guid.NewGuid().ToString("N"), new PowerScriptTransformInput(), noConsent: true, timeout.Token);
        Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, result.ExitCode);
        StringAssert.Contains(result.StdErr, "installed script ID");
        await client.ListAsync(timeout.Token);
        client.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => client.ListAsync(timeout.Token));
    }

    [TestMethod]
    public async Task FramingRejectsOversizedAndTruncatedMessagesAndHonorsCancellation()
    {
        var oversized = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(oversized, PowerScriptsSessionProtocol.MaxFrameBytes + 1);
        using var invalid = new MemoryStream(oversized);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => PowerScriptsSessionProtocol.ReadAsync(invalid));
        using var truncated = new MemoryStream([2, 0, 0, 0, 42]);
        await Assert.ThrowsExceptionAsync<EndOfStreamException>(() => PowerScriptsSessionProtocol.ReadAsync(truncated));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var empty = new MemoryStream();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => PowerScriptsSessionProtocol.ReadAsync(empty, cancellation.Token));
    }
}

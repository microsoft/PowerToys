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
                StringAssert.Contains(refused.RootElement.GetProperty("stderr").GetString(), "Host sessions accept");
            }

            using (var state = await Request("consumers", "list"))
            {
                Assert.AreEqual(PowerScriptsProtocol.ExitCode.Ok, state.RootElement.GetProperty("exitCode").GetInt32());
                using var snapshot = JsonDocument.Parse(state.RootElement.GetProperty("stdout").GetString()!);
                Assert.IsTrue(snapshot.RootElement.TryGetProperty("consumers", out _));
            }

            using (var refused = await Request("consumers", "set", "command-palette", "off"))
            {
                Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, refused.RootElement.GetProperty("exitCode").GetInt32());
            }

            foreach (var verb in new[] { "run", "transform" })
            {
                using var failure = await Request(verb, "missing-" + Guid.NewGuid().ToString("N"), "--consumer", "command-line", "--root", root);
                Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, failure.RootElement.GetProperty("exitCode").GetInt32());
                StringAssert.Contains(failure.RootElement.GetProperty("stderr").GetString(), "installed script ID");
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
        var result = await client.TransformAsync("missing-" + Guid.NewGuid().ToString("N"), new PowerScriptTransformInput(), cancellationToken: timeout.Token);
        Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, result.ExitCode);
        StringAssert.Contains(result.StdErr, "installed script ID");
        await client.ListAsync(timeout.Token);
        client.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => client.ListAsync(timeout.Token));
    }

    [TestMethod]
    public async Task ExecutionPoolReusesHostsWithoutReplayingInterruptedRequests()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-pool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var pool = CreatePool();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var first = await pool.InvokeAsync(["echo"], "first payload", timeout.Token);
            var second = await pool.InvokeAsync(["echo"], "second payload", timeout.Token);
            Assert.AreEqual(first.StdOut, second.StdOut, "Sequential executions must reuse the warm Host.");
            Assert.AreEqual("second payload", second.StdErr, "A request must not retain the preceding input.");

            var marker = Path.Combine(root, "delivered.txt");
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => pool.InvokeAsync(["exit", marker], null, timeout.Token));
            Assert.AreEqual(1, File.ReadAllLines(marker).Length, "A delivered invocation must never be replayed.");
            var recovered = await pool.InvokeAsync(["echo"], null, timeout.Token);
            Assert.AreNotEqual(first.StdOut, recovered.StdOut, "A later request must start a new Host after failure.");
        }
        finally
        {
            pool.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExecutionPoolDoesNotSerializeOrCrossCancelIndependentRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-parallel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var pool = CreatePool();
        using var stop = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var firstStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, timeout.Token);
        try
        {
            var firstReady = Path.Combine(root, "first.txt");
            var secondReady = Path.Combine(root, "second.txt");
            var release = Path.Combine(root, "release.txt");
            var first = pool.InvokeAsync(["block", firstReady, release], null, firstStop.Token);
            var second = pool.InvokeAsync(["block", secondReady, release], "independent input", timeout.Token);
            while (!File.Exists(firstReady) || !File.Exists(secondReady))
            {
                await Task.Delay(10, timeout.Token);
            }

            Assert.AreNotEqual(File.ReadAllText(firstReady), File.ReadAllText(secondReady));
            stop.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => first);
            Assert.IsFalse(second.IsCompleted, "Cancelling one execution must not terminate another Host.");
            File.WriteAllText(release, string.Empty);
            var result = await second;
            Assert.AreEqual("independent input", result.StdErr);
            Assert.AreEqual(PowerScriptsProtocol.ExitCode.Ok, result.ExitCode);
        }
        finally
        {
            pool.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    private static PowerScriptsHostPool CreatePool() => new(() => new ProcessStartInfo(
        Path.Combine(AppContext.BaseDirectory, "session-host", "SessionHost.exe"))
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    });

    [TestMethod]
    public async Task SessionStopsWhenOwnerExitsEvenWhileInputRemainsOpen()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var controllerStart = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "session-host", "SessionHost.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var controller = Process.Start(controllerStart) ?? throw new InvalidOperationException("Controller did not start.");
        var start = new ProcessStartInfo(PowerScriptsClient.ResolveHostPath() ?? throw new FileNotFoundException("Host missing."))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("session");
        start.ArgumentList.Add("--owner");
        start.ArgumentList.Add(controller.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var host = Process.Start(start) ?? throw new InvalidOperationException("Host did not start.");
        var stderr = host.StandardError.ReadToEndAsync();
        try
        {
            await PowerScriptsSessionProtocol.WriteAsync(
                host.StandardInput.BaseStream,
                PowerScriptsSessionProtocol.CreateRequest(["consumers", "list"], null),
                timeout.Token);
            using var response = JsonDocument.Parse(await PowerScriptsSessionProtocol.ReadAsync(host.StandardOutput.BaseStream, timeout.Token)
                ?? throw new EndOfStreamException("Session terminated before owner exit."));
            Assert.AreEqual(PowerScriptsProtocol.ExitCode.Ok, response.RootElement.GetProperty("exitCode").GetInt32(), response.RootElement.GetProperty("stderr").GetString());
            controller.Kill(entireProcessTree: true);
            await controller.WaitForExitAsync(timeout.Token);
            await host.WaitForExitAsync(timeout.Token);
            Assert.AreNotEqual(0, host.ExitCode, await stderr);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                await host.WaitForExitAsync();
            }

            if (!controller.HasExited)
            {
                controller.Kill(entireProcessTree: true);
                await controller.WaitForExitAsync();
            }
        }
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

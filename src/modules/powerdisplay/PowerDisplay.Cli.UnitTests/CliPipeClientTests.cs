// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Ipc;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Tests for <see cref="CliPipeClient"/>.
/// </summary>
[TestClass]
public class CliPipeClientTests
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);

    // ── Happy-path: in-proc fake server, trusted verifier ─────────────────────
    [TestMethod]
    [Timeout(10_000)]
    public async Task SendAsync_WithFakeServerAndTrustedVerifier_ReturnsCannedResponse()
    {
        const string RequestJson = @"{""command"":""list""}";
        const string ResponseJson = @"{""monitors"":[]}";

        // Start a one-shot in-proc server on the same pipe name
        using var serverReady = new SemaphoreSlim(0, 1);
        var serverTask = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(
                PipeNames.CliServer(),
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            serverReady.Release(); // signal: server is now listening
            await server.WaitForConnectionAsync();

            // Mirror the server protocol: BOM-less UTF-16 LE (same as CliPipeClient / CliPipeServer).
            // Use the shared pipe encoding/buffer so the fake server stays byte-compatible with the client.
            using var reader = new StreamReader(server, CliPipeProtocol.PipeEncoding, false, CliPipeProtocol.BufferSize, leaveOpen: true);
            using var writer = new StreamWriter(server, CliPipeProtocol.PipeEncoding, CliPipeProtocol.BufferSize, leaveOpen: true) { AutoFlush = true };

            _ = await reader.ReadLineAsync();

            // Echo back the canned response regardless of what was sent
            await writer.WriteLineAsync(ResponseJson);
        });

        // Wait until the server is listening before connecting
        await serverReady.WaitAsync(TimeSpan.FromSeconds(5));

        // The in-proc fake server is not the real sibling PowerToys.PowerDisplay.exe, so the
        // real production verifier would reject it. Inject a trusted stub via the internal
        // constructor to exercise the round trip without production bypasses.
        var client = new CliPipeClient(static _ => true);
        var result = await client.SendAsync(RequestJson, ConnectTimeout, CancellationToken.None);

        await serverTask; // ensure the server task completes cleanly

        Assert.AreEqual(ResponseJson, result);
    }

    // ── Untrusted server: verifier rejects, no request body is ever sent ─────
    [TestMethod]
    [Timeout(10_000)]
    public async Task SendAsync_WithUntrustedVerifier_ReturnsNullAndSendsNoRequestBody()
    {
        const string RequestJson = @"{""command"":""list""}";

        using var serverReady = new SemaphoreSlim(0, 1);
        string? receivedLine = "not-read-yet";
        var serverTask = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(
                PipeNames.CliServer(),
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            serverReady.Release();
            await server.WaitForConnectionAsync();

            using var reader = new StreamReader(server, CliPipeProtocol.PipeEncoding, false, CliPipeProtocol.BufferSize, leaveOpen: true);

            // The client must close the connection right after a failed verification and before
            // writing anything, so the read reaches end-of-stream (null) instead of returning a line.
            receivedLine = await reader.ReadLineAsync();
        });

        await serverReady.WaitAsync(TimeSpan.FromSeconds(5));

        var client = new CliPipeClient(static _ => false);
        var result = await client.SendAsync(RequestJson, ConnectTimeout, CancellationToken.None);

        await serverTask;

        Assert.IsNull(result, "Expected null when the connected server fails identity verification");
        Assert.IsNull(receivedLine, "Expected no request body to reach an untrusted server");
    }

    // ── No-server path: returns null within short timeout ────────────────────
    [TestMethod]
    [Timeout(5_000)]
    public async Task SendAsync_NoServer_ReturnsNullWithinShortTimeout()
    {
        // There is no server listening on this pipe, so ConnectAsync will throw TimeoutException.
        // We use ShortTimeout (200 ms) to keep the test fast.
        var client = new CliPipeClient();
        var result = await client.SendAsync(@"{""command"":""list""}", ShortTimeout, CancellationToken.None);

        Assert.IsNull(result, "Expected null when no pipe server is running");
    }

    // ── Cancellation propagates ───────────────────────────────────────────────
    [TestMethod]
    [Timeout(5_000)]
    public async Task SendAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        var client = new CliPipeClient();

        // Assert.ThrowsExceptionAsync<T> matches the exact type, so TaskCanceledException
        // (which derives from OperationCanceledException) would fail it.  Use a manual
        // try/catch so any subclass of OperationCanceledException is accepted.
        try
        {
            await client.SendAsync(@"{""command"":""list""}", ConnectTimeout, cts.Token);
            Assert.Fail("Expected the operation to be cancelled.");
        }
        catch (OperationCanceledException)
        {
            // expected (TaskCanceledException derives from OperationCanceledException)
        }
    }

    // ── PipeServerIdentity: exact sibling-path comparison ─────────────────────
    [TestMethod]
    public void PathsMatch_ExactPathDifferentCasing_ReturnsTrue()
    {
        const string Expected = @"C:\Program Files\PowerToys\PowerToys.PowerDisplay.exe";
        const string Actual = @"c:\program files\powertoys\POWERTOYS.POWERDISPLAY.EXE";

        Assert.IsTrue(PipeServerIdentity.PathsMatch(Actual, Expected));
    }

    [TestMethod]
    public void PathsMatch_EquivalentFullPathsContainingDotSegments_ReturnsTrue()
    {
        const string Expected = @"C:\Program Files\PowerToys\PowerToys.PowerDisplay.exe";
        const string Actual = @"C:\Program Files\PowerToys\.\Subfolder\..\PowerToys.PowerDisplay.exe";

        Assert.IsTrue(PipeServerIdentity.PathsMatch(Actual, Expected));
    }

    [TestMethod]
    public void PathsMatch_DifferentDirectory_ReturnsFalse()
    {
        const string Expected = @"C:\Program Files\PowerToys\PowerToys.PowerDisplay.exe";
        const string Actual = @"C:\Some\Other\Place\PowerToys.PowerDisplay.exe";

        Assert.IsFalse(PipeServerIdentity.PathsMatch(Actual, Expected));
    }

    [TestMethod]
    public void PathsMatch_DifferentFileName_ReturnsFalse()
    {
        const string Expected = @"C:\Program Files\PowerToys\PowerToys.PowerDisplay.exe";
        const string Actual = @"C:\Program Files\PowerToys\PowerToys.NotPowerDisplay.exe";

        Assert.IsFalse(PipeServerIdentity.PathsMatch(Actual, Expected));
    }

    // ── PipeServerIdentity: real GetNamedPipeServerProcessId + QueryFullProcessImageNameW round trip ──
    [TestMethod]
    [Timeout(10_000)]
    public async Task IsTrustedServer_SelfConnectedPipeWithMatchingExpectedPath_ReturnsTrue()
    {
        string selfPipeName = $"PowerDisplay_Cli_UnitTests_{Guid.NewGuid():N}";
        string currentProcessPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Environment.ProcessPath is unexpectedly null for the current test host process.");

        using var server = new NamedPipeServerStream(selfPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", selfPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var acceptTask = server.WaitForConnectionAsync();
        await client.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, CancellationToken.None);
        await acceptTask;

        // The pipe server here is the current test-host process itself, so the expected path is
        // this process's own image path when verifying the client-side connected stream.
        Assert.IsTrue(PipeServerIdentity.IsTrustedServer(client, currentProcessPath));
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task IsTrustedServer_SelfConnectedPipeWithMismatchedExpectedPath_ReturnsFalse()
    {
        string selfPipeName = $"PowerDisplay_Cli_UnitTests_{Guid.NewGuid():N}";

        using var server = new NamedPipeServerStream(selfPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", selfPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var acceptTask = server.WaitForConnectionAsync();
        await client.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, CancellationToken.None);
        await acceptTask;

        Assert.IsFalse(PipeServerIdentity.IsTrustedServer(client, @"C:\definitely\not\the\real\process.exe"));
    }
}

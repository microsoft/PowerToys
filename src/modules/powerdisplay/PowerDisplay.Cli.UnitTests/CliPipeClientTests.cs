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

    // ── Happy-path: in-proc fake server ──────────────────────────────────────
    [TestMethod]
    [Timeout(10_000)]
    public async Task SendAsync_WithFakeServer_ReturnsCannedResponse()
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

            var line = await reader.ReadLineAsync();

            // Echo back the canned response regardless of what was sent
            await writer.WriteLineAsync(ResponseJson);
        });

        // Wait until the server is listening before connecting
        await serverReady.WaitAsync(TimeSpan.FromSeconds(5));

        var client = new CliPipeClient();
        var result = await client.SendAsync(RequestJson, ConnectTimeout, CancellationToken.None);

        await serverTask; // ensure the server task completes cleanly

        Assert.AreEqual(ResponseJson, result);
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
}

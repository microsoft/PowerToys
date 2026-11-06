// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Ipc;

/// <summary>
/// CLI-side named-pipe client that connects to the running PowerDisplay app, sends one request
/// line, reads one response line, and returns <see langword="null"/> on connect failure or timeout.
/// <para>
/// <b>Protocol:</b> BOM-less UTF-16 LE encoding, <c>'\n'</c>-delimited lines, one request → one response.
/// Mirrors the app-side <c>CliPipeServer</c> in <c>PowerDisplay.Ipc/CliPipeServer.cs</c>.
/// </para>
/// </summary>
public sealed class CliPipeClient
{
    private readonly Func<NamedPipeClientStream, bool> _verifyServer;

    /// <summary>
    /// Creates a client that authenticates the connected server via <see cref="PipeServerIdentity.IsTrustedServer(PipeStream)"/>.
    /// </summary>
    public CliPipeClient()
        : this(PipeServerIdentity.IsTrustedServer)
    {
    }

    /// <summary>
    /// Test-only constructor that injects a stand-in verifier so unit tests can accept or reject
    /// an in-process fake server without bypassing verification in production code.
    /// </summary>
    /// <param name="verifyServer">Returns <see langword="true"/> when the connected pipe's server process is trusted.</param>
    internal CliPipeClient(Func<NamedPipeClientStream, bool> verifyServer)
    {
        _verifyServer = verifyServer ?? throw new ArgumentNullException(nameof(verifyServer));
    }

    /// <summary>
    /// Connects to the PowerDisplay named-pipe server, sends <paramref name="requestJson"/>,
    /// and returns the response JSON line.
    /// </summary>
    /// <param name="requestJson">The JSON-encoded request to send.</param>
    /// <param name="connectTimeout">How long to wait for the pipe server to accept the connection.</param>
    /// <param name="ct">Cancellation token; <see cref="OperationCanceledException"/> propagates to the caller.</param>
    /// <returns>
    /// The response JSON line on success; <see langword="null"/> when the app is not running,
    /// the pipe is unavailable, the connection timed out, or the connected server fails identity
    /// verification (i.e. it is not the sibling <c>PowerToys.PowerDisplay.exe</c>).
    /// </returns>
    public async Task<string?> SendAsync(string requestJson, TimeSpan connectTimeout, CancellationToken ct)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeNames.CliServer(), PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync((int)connectTimeout.TotalMilliseconds, ct);

            // Authenticate the connected server before any request bytes are written, so an
            // untrusted process never receives the request payload.
            if (!_verifyServer(client))
            {
                return null;
            }

            using var writer = new StreamWriter(client, CliPipeProtocol.PipeEncoding, CliPipeProtocol.BufferSize, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, CliPipeProtocol.PipeEncoding, false, CliPipeProtocol.BufferSize, leaveOpen: true);

            await writer.WriteLineAsync(requestJson.AsMemory(), ct);
            return await reader.ReadLineAsync(ct);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        // OperationCanceledException is intentionally NOT caught here — it propagates to the
        // caller, which treats Ctrl+C / timeout-token cancellation as user cancellation.
    }
}

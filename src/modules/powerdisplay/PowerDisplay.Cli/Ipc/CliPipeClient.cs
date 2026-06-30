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
/// Mirrors the app-side <c>CliPipeServer</c> in <c>PowerDisplay/Ipc/CliPipeServer.cs</c>.
/// </para>
/// </summary>
public sealed class CliPipeClient
{
    /// <summary>
    /// Connects to the PowerDisplay named-pipe server, sends <paramref name="requestJson"/>,
    /// and returns the response JSON line.
    /// </summary>
    /// <param name="requestJson">The JSON-encoded request to send.</param>
    /// <param name="connectTimeout">How long to wait for the pipe server to accept the connection.</param>
    /// <param name="ct">Cancellation token; <see cref="OperationCanceledException"/> propagates to the caller.</param>
    /// <returns>
    /// The response JSON line on success; <see langword="null"/> when the app is not running,
    /// the pipe is unavailable, or the connection timed out.
    /// </returns>
    public async Task<string?> SendAsync(string requestJson, TimeSpan connectTimeout, CancellationToken ct)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeNames.CliServer(), PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ToConnectMilliseconds(connectTimeout), ct);

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

    /// <summary>
    /// Converts a connect-timeout <see cref="TimeSpan"/> to the int-milliseconds value
    /// <see cref="NamedPipeClientStream.ConnectAsync(int, CancellationToken)"/> expects, clamped to
    /// <c>[0, int.MaxValue]</c>. A large <c>--timeout</c> (e.g. millions of seconds) would otherwise
    /// overflow the cast to a negative value, which <c>ConnectAsync</c> rejects with
    /// <see cref="ArgumentOutOfRangeException"/> — surfacing as exit 9 instead of a clean connect/timeout.
    /// int.MaxValue ms (~24.8 days) is effectively unbounded for a connect wait.
    /// </summary>
    internal static int ToConnectMilliseconds(TimeSpan timeout)
    {
        var ms = timeout.TotalMilliseconds;
        if (ms >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return ms < 0 ? 0 : (int)ms;
    }
}

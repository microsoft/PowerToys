// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Contracts;

namespace PowerDisplay.Ipc;

/// <summary>
/// App-side named-pipe server that accepts CLI connections and dispatches each request through
/// <see cref="CliRequestHandler"/>.
/// <para>
/// <b>Protocol:</b> One connection = one request/response exchange. The server reads one
/// <c>'\n'</c>-delimited JSON line, calls <see cref="CliRequestHandler.HandleAsync"/>, writes one
/// JSON line back, then closes the connection. Unicode encoding mirrors
/// <c>PowerDisplay/Helpers/NamedPipeProcessor.cs</c>.
/// </para>
/// <para>
/// <b>ACL:</b> Uses <see cref="NamedPipeServerStreamAcl.Create"/> with a
/// <see cref="PipeSecurity"/> that grants <see cref="WellKnownSidType.AuthenticatedUserSid"/>
/// <c>ReadWrite | CreateNewInstance</c>, so a same-user non-elevated CLI can connect to an
/// elevated app (cross-integrity). Pattern sourced from
/// <c>MouseWithoutBorders/App/Class/IClipboardHelper.cs – IpcChannel&lt;T&gt;.StartIpcServer</c>.
/// </para>
/// <para>
/// <b>Concurrency:</b> <see cref="NamedPipeServerStream.MaxAllowedServerInstances"/> allows the
/// OS to serve concurrent CLI calls without queueing at the kernel level. Each accept iteration
/// creates a fresh server-stream instance, so connections don't block each other.
/// </para>
/// </summary>
public sealed class CliPipeServer
{
    private readonly CliRequestHandler _handler;

    /// <summary>
    /// Initialises the server with the request handler that will be called for each connection.
    /// </summary>
    /// <param name="handler">The handler that processes each request. Must not be null.</param>
    public CliPipeServer(CliRequestHandler handler)
        => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>
    /// Starts the background accept loop. Fire-and-forget: returns immediately; the loop runs
    /// until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Token that stops the server when cancelled.</param>
    public void Start(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => AcceptLoopAsync(cancellationToken), cancellationToken);
    }

    // ─── Private implementation ───────────────────────────────────────────────
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        var pipeName = PipeNames.CliServer();

        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        Logger.LogInfo($"[PowerDisplay CLI IPC] Server starting on pipe '{pipeName}'");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = NamedPipeServerStreamAcl.Create(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    security);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                await ServeOneAsync(server, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PowerDisplay CLI IPC] server loop error: {ex.GetType().Name}: {ex.Message}");

                // Continue — a single bad connection must not kill the server.
            }
        }

        Logger.LogInfo("[PowerDisplay CLI IPC] Server stopped.");
    }

    private async Task ServeOneAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        // leaveOpen: true — the pipe stream is owned by the caller; disposing reader/writer
        // must not close it prematurely.
        using var reader = new StreamReader(server, CliPipeProtocol.PipeEncoding, detectEncodingFromByteOrderMarks: false, bufferSize: CliPipeProtocol.BufferSize, leaveOpen: true);
        using var writer = new StreamWriter(server, CliPipeProtocol.PipeEncoding, bufferSize: CliPipeProtocol.BufferSize, leaveOpen: true) { AutoFlush = true };

        var requestJson = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(requestJson))
        {
            Logger.LogWarning("[PowerDisplay CLI IPC] Received empty/null request line; closing connection.");
            return;
        }

        var responseJson = await _handler.HandleAsync(requestJson, ct).ConfigureAwait(false);
        await writer.WriteLineAsync(responseJson.AsMemory(), ct).ConfigureAwait(false);
    }
}

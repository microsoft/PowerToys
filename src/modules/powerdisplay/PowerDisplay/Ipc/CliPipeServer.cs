// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
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
/// <see cref="PipeSecurity"/> that grants the <em>current user's</em> SID
/// <c>ReadWrite | CreateNewInstance</c>, so a same-user non-elevated CLI can connect to a
/// same-user elevated app (elevation changes the integrity level, not the user SID). The ACE is
/// deliberately scoped to the owner rather than
/// <see cref="WellKnownSidType.AuthenticatedUserSid"/>: named pipes are not session-isolated (the
/// session id in <see cref="PipeNames.CliServer"/> only avoids name collisions), so an
/// AuthenticatedUsers ACE would let any other logged-on user drive this user's monitors. Pattern
/// sourced from <c>MouseWithoutBorders/App/Class/IClipboardHelper.cs – IpcChannel&lt;T&gt;.StartIpcServer</c>.
/// </para>
/// <para>
/// <b>Concurrency:</b> the accept loop serves one request at a time — it waits for a connection,
/// runs it to completion, then accepts the next. This is sufficient for the one-shot CLI client.
/// To keep the well-known pipe name owned continuously (see <see cref="CreateServerStream"/>), the
/// replacement instance is created before the just-served one is disposed, so up to two instances
/// exist briefly; <see cref="NamedPipeServerStream.MaxAllowedServerInstances"/> is passed to allow
/// that overlap, not to serve requests concurrently.
/// </para>
/// <para>
/// <b>Limitation:</b> because the loop is single-instance, one in-flight request holds the sole
/// pipe instance until <see cref="CliRequestHandler.HandleAsync"/> returns. A blocking DDC/CI
/// hardware write cannot be cancelled mid-call (the underlying Win32 <c>SetVCPFeature</c> I2C
/// transaction is synchronous), so a slow or hung monitor serializes every subsequent CLI request
/// behind it until the OS DDC/CI layer times out. This is an accepted trade-off for the one-shot
/// CLI; making the server handle connections concurrently would require guarding the shared
/// ViewModel / MonitorManager state the handler currently touches single-threaded.
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

        // Scope pipe access to the current user's SID (not AuthenticatedUsers). Elevation changes the
        // integrity level, not the user SID, so a same-user non-elevated CLI can still connect to a
        // same-user elevated app, while other logged-on users are denied (named pipes are not
        // session-isolated). Fall back to AuthenticatedUsers only if the owner SID is somehow null.
        using var currentIdentity = WindowsIdentity.GetCurrent();
        var ownerSid = currentIdentity.User
            ?? new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            ownerSid,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        Logger.LogInfo($"[PowerDisplay CLI IPC] Server starting on pipe '{pipeName}'");

        // Keep one instance of the well-known pipe name alive at all times so it is never left
        // unowned between requests. Only the FIRST instance uses PipeOptions.FirstPipeInstance, which
        // fails loudly if another (possibly malicious) process already owns the predictable,
        // session-scoped name at startup. Every later instance is stood up BEFORE the just-served one
        // is disposed, so at least one instance always holds the name — closing the gap a per-request
        // create/dispose would otherwise reopen after every request. Without this, a same- or
        // cross-user process could win that gap with CreateNamedPipe, take the name under its own ACL,
        // and thereafter both deny the real server (its FirstPipeInstance create would fail forever)
        // and intercept/spoof CLI traffic (the client connects by name only, it does not verify the
        // server).
        NamedPipeServerStream? listener = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // First create (startup, or post-error recovery when no instance of ours is alive)
                    // asserts first-instance ownership; the overlap create below deliberately does not.
                    listener ??= CreateServerStream(pipeName, security, firstInstance: true);

                    await listener.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    // A client is connected to `listener`. Stand up the replacement instance now, while
                    // the connected one still holds the name, so ownership is continuous; then serve
                    // and dispose the connected instance.
                    var connected = listener;
                    listener = CreateServerStream(pipeName, security, firstInstance: false);

                    try
                    {
                        await ServeOneAsync(connected, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        connected.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[PowerDisplay CLI IPC] server loop error: {ex.GetType().Name}: {ex.Message}");

                    // Drop the (possibly broken) current instance and back off before retrying, so a
                    // persistent create failure — e.g. another process holding the name, which
                    // FirstPipeInstance surfaces as an exception — does not spin the loop. The next
                    // iteration recreates with FirstPipeInstance (no instance of ours is alive here).
                    listener?.Dispose();
                    listener = null;

                    try
                    {
                        await Task.Delay(500, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            listener?.Dispose();
        }

        Logger.LogInfo("[PowerDisplay CLI IPC] Server stopped.");
    }

    /// <summary>
    /// Creates one server-stream instance for the CLI pipe. Only the first instance for the process
    /// lifetime passes <see cref="PipeOptions.FirstPipeInstance"/> (to fail loudly if the predictable,
    /// session-scoped name is already owned by another process at startup); every subsequent instance
    /// omits it, because by design an instance of ours is always already alive when the next is
    /// created, so <see cref="PipeOptions.FirstPipeInstance"/> would spuriously fail. Internal so the
    /// ownership mechanic can be unit-tested.
    /// </summary>
    internal static NamedPipeServerStream CreateServerStream(string pipeName, PipeSecurity security, bool firstInstance)
    {
        var options = PipeOptions.Asynchronous;
        if (firstInstance)
        {
            options |= PipeOptions.FirstPipeInstance;
        }

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
    }

    private async Task ServeOneAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        // leaveOpen: true — the pipe stream is owned by the caller; disposing reader/writer
        // must not close it prematurely.
        using var reader = new StreamReader(server, CliPipeProtocol.PipeEncoding, detectEncodingFromByteOrderMarks: false, bufferSize: CliPipeProtocol.BufferSize, leaveOpen: true);
        using var writer = new StreamWriter(server, CliPipeProtocol.PipeEncoding, bufferSize: CliPipeProtocol.BufferSize, leaveOpen: true) { AutoFlush = true };

        // Bound the read by both time and length so a client that connects but never sends a
        // (complete) line cannot stall the single-threaded accept loop or balloon memory.
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readCts.CancelAfter(CliPipeProtocol.ReadTimeoutMilliseconds);

        string? requestJson;
        try
        {
            requestJson = await ReadBoundedLineAsync(reader, CliPipeProtocol.MaxRequestChars, readCts.Token).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            Logger.LogWarning($"[PowerDisplay CLI IPC] Request exceeded {CliPipeProtocol.MaxRequestChars} chars; closing connection.");
            return;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Read timeout (not app shutdown — that propagates to break the accept loop).
            Logger.LogWarning("[PowerDisplay CLI IPC] Request read timed out; closing connection.");
            return;
        }

        if (string.IsNullOrEmpty(requestJson))
        {
            Logger.LogWarning("[PowerDisplay CLI IPC] Received empty/null request line; closing connection.");
            return;
        }

        var responseJson = await _handler.HandleAsync(requestJson, ct).ConfigureAwait(false);

        // Bound the write + drain the same way the read is bounded above. The pipe uses a 0-byte
        // output buffer, so both WriteLineAsync and WaitForPipeDrain block until the client reads;
        // a connected client that never reads must not wedge the single-threaded accept loop.
        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        writeCts.CancelAfter(CliPipeProtocol.WriteTimeoutMilliseconds);

        // Drain rationale: without WaitForPipeDrain, disposing the handle immediately after an
        // AutoFlush write can truncate a large response the client has not finished reading,
        // surfacing as a spurious deserialize-mismatch on the CLI side. WaitForPipeDrain has no
        // timeout/CancellationToken overload, so run it on a worker and bound it via writeCts;
        // disposing the pipe (the caller's `using`) unblocks a still-waiting worker.
        try
        {
            await writer.WriteLineAsync(responseJson.AsMemory(), writeCts.Token).ConfigureAwait(false);

            var drainTask = Task.Run(
                () =>
                {
                    try
                    {
                        server.WaitForPipeDrain();
                    }
                    catch (IOException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                },
                CancellationToken.None);

            await drainTask.WaitAsync(writeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Write/drain timeout (not app shutdown — that propagates to break the accept loop).
            Logger.LogWarning("[PowerDisplay CLI IPC] Response write/drain timed out; closing connection.");
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Reads one <c>'\n'</c>-delimited line, swallowing a trailing <c>'\r'</c>, but never buffering
    /// more than <paramref name="maxChars"/> characters. Returns the line (without its terminator),
    /// or <see langword="null"/> at end-of-stream with no data. Throws <see cref="InvalidDataException"/>
    /// when the line would exceed <paramref name="maxChars"/>.
    /// </summary>
    internal static async Task<string?> ReadBoundedLineAsync(TextReader reader, int maxChars, CancellationToken ct)
    {
        var builder = new StringBuilder();
        var buffer = new char[CliPipeProtocol.BufferSize];

        while (true)
        {
            // Honour the read deadline / app shutdown even if the underlying reader does not observe
            // the token between chunks.
            ct.ThrowIfCancellationRequested();

            int read = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0)
            {
                // End of stream: null when nothing was read, otherwise the (unterminated) tail.
                return builder.Length == 0 ? null : builder.ToString();
            }

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (c == '\n')
                {
                    return builder.ToString();
                }

                if (c == '\r')
                {
                    continue;
                }

                if (builder.Length >= maxChars)
                {
                    throw new InvalidDataException("CLI request line exceeded the maximum allowed length.");
                }

                builder.Append(c);
            }
        }
    }
}

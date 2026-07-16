// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using PowerDisplay.ViewModels;

namespace PowerDisplay.Ipc;

/// <summary>
/// App-side IPC dispatcher. Called from the named-pipe server (Task 3.1) on a background thread.
/// <para>
/// <b>Threading:</b> ViewModel/MonitorManager access is <em>initiated</em> on the UI thread via the
/// injected <see cref="DispatcherQueue"/> using a <see cref="TaskCompletionSource{T}"/> pattern
/// (see <c>RunOnUiThreadAsync</c>): the synchronous VM snapshot reads and the dispatch of each
/// hardware write run on the UI thread. The work is awaited with <c>ConfigureAwait(false)</c>, so
/// continuations after an incomplete hardware-write await resume on a thread-pool thread — the
/// MonitorManager controllers are already thread-affinity-free, matching the pattern established by
/// <c>ApplyLightSwitchProfile</c> in <see cref="MainViewModel"/>. Only serialization of the
/// resulting DTO happens on the background thread.
/// </para>
/// <para>
/// <b>Error contract:</b> <see cref="HandleAsync"/> never throws. Client cancellation or deadline
/// expiry is not propagated to the handler; it only closes the client pipe. Cancellation of the
/// server-lifetime token (or any token-aware server operation that observes it) maps to
/// <see cref="CliErrorCodes.Timeout"/> / exit 8. Any other unexpected exception is reported as
/// <see cref="CliErrorCodes.InternalError"/> / exit 9.
/// </para>
/// </summary>
public sealed class CliRequestHandler : ICliRequestProcessor
{
    private readonly MainViewModel _vm;
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>
    /// Initialises the handler with the live <see cref="MainViewModel"/> and the WinUI dispatcher
    /// that owns the ViewModel's data (Monitors, MonitorManager, settings utils).
    /// </summary>
    /// <param name="vm">The app's main view-model. Must not be null.</param>
    /// <param name="dispatcherQueue">
    /// The UI-thread dispatcher that owns <paramref name="vm"/>. Must not be null.
    /// </param>
    public CliRequestHandler(MainViewModel vm, DispatcherQueue dispatcherQueue)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the JSON <paramref name="requestJson"/>, routes it to the matching
    /// <see cref="ICliCommandHandler"/> on the UI thread, and returns the serialized response JSON.
    /// </summary>
    /// <param name="requestJson">One-line JSON request from the pipe client.</param>
    /// <param name="cancellationToken">Cancellation token observed by the server-lifetime operation.</param>
    /// <returns>
    /// A one-line JSON string. Always a valid response — never throws. Cancellation maps to
    /// <see cref="CliErrorCodes.Timeout"/>; any other unexpected exception maps to
    /// <see cref="CliErrorCodes.InternalError"/>.
    /// </returns>
    public async Task<string> HandleAsync(string requestJson, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleCoreAsync(requestJson, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation while marshalling onto the UI thread (before the command ran). Mirror the
            // command-execution timeout contract: report TIMEOUT (exit 8), not INTERNAL_ERROR.
            return CliRequestDispatcher.CreateTimeoutResponse();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[CliRequestHandler] Unexpected exception in HandleAsync: {ex.GetType().Name}: {ex.Message}");
            return CliRequestDispatcher.CreateInternalErrorResponse(ex.Message);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────
    private async Task<string> HandleCoreAsync(string requestJson, CancellationToken ct)
    {
        CliRequestEnvelope? envelope = null;
        try
        {
            envelope = JsonSerializer.Deserialize(requestJson, ContractsJsonContext.Default.CliRequestEnvelope);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning($"[CliRequestHandler] Failed to parse request JSON: {ex.Message}");
        }

        if (envelope is null || string.IsNullOrEmpty(envelope.Command))
        {
            return CliRequestDispatcher.CreateInvalidEnvelopeResponse();
        }

        // Marshal all ViewModel/MonitorManager access onto the UI thread.
        // Serialization of the resulting string happens back on this background thread.
        return await RunOnUiThreadAsync(async () =>
        {
            // Snapshot VM state on the UI thread — these reads touch _settingsUtils and
            // _monitors which are UI-thread-owned. Profiles are loaded lazily (only the
            // 'profiles' command pays for the disk read).
            var snapshot = _vm.SnapshotMonitors();
            var hiddenIds = _vm.GetHiddenMonitorIds();
            var customMappings = _vm.CustomVcpMappings;
            var manager = _vm.MonitorManager;

            return await CliRequestDispatcher.BuildResponseAsync(
                envelope,
                snapshot,
                hiddenIds,
                customMappings,
                manager,
                _vm.MouseWheelIncrement,
                ProfileHelper.LoadProfilesAsync,
                (id, token) => _vm.ApplyProfileForCliAsync(id, token),
                ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Marshals async work onto the UI dispatcher thread and returns the result on the calling
    /// background thread. Uses the <see cref="TaskCompletionSource{T}"/> + <c>TryEnqueue</c>
    /// pattern established by <c>ApplyLightSwitchProfile</c> in <see cref="MainViewModel"/>.
    /// </summary>
    private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> work)
    {
        System.Diagnostics.Debug.Assert(!_dispatcherQueue.HasThreadAccess, "HandleAsync must be called from a background thread, not the UI thread");

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var result = await work().ConfigureAwait(false);
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException ex)
            {
                tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!enqueued)
        {
            Logger.LogError("[CliRequestHandler] Failed to enqueue work to UI thread — dispatcher may be shutting down");
            tcs.TrySetException(new InvalidOperationException("UI dispatcher could not accept work (app may be shutting down)"));
        }

        return tcs.Task;
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using PowerDisplay.ViewModels;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// App-side IPC dispatcher. Called from the named-pipe server (Task 3.1) on a background thread.
/// <para>
/// <b>Threading:</b> All ViewModel/MonitorManager access is marshalled onto the UI thread via the
/// injected <see cref="DispatcherQueue"/> using a <see cref="TaskCompletionSource{T}"/> pattern
/// (see <c>RunOnUiThreadAsync</c>). Only serialization of the resulting DTO happens on the
/// background thread; this matches the pattern established by <c>ApplyLightSwitchProfile</c> in
/// <see cref="MainViewModel"/>.
/// </para>
/// <para>
/// <b>Error contract:</b> <see cref="HandleAsync"/> never throws. Any unexpected exception is
/// caught and returned as a serialized <see cref="CliErrorResult"/> with
/// <see cref="CliErrorCodes.InternalError"/> / <see cref="CliExitCodes.InternalError"/> (exit 9).
/// </para>
/// </summary>
public sealed class CliRequestHandler
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
    /// Parses the JSON <paramref name="requestJson"/>, dispatches it to the appropriate
    /// projector/executor on the UI thread, and returns the serialized response JSON.
    /// </summary>
    /// <param name="requestJson">One-line JSON request from the pipe client.</param>
    /// <param name="ct">Cancellation token (Ctrl-C / server timeout).</param>
    /// <returns>
    /// A one-line JSON string. Always a valid response — never throws. On any unexpected exception,
    /// returns a <see cref="CliErrorResult"/> with code <see cref="CliErrorCodes.InternalError"/>.
    /// </returns>
    public async Task<string> HandleAsync(string requestJson, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(requestJson, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation during execution — return timeout error
            var timeoutErr = MakeError("unknown", CliErrorCodes.InternalError, "request was cancelled");
            return Serialize(timeoutErr);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[CliRequestHandler] Unexpected exception in HandleAsync: {ex.GetType().Name}: {ex.Message}");
            var errorResult = MakeError("unknown", CliErrorCodes.InternalError, $"unexpected error: {ex.Message}");
            return Serialize(errorResult);
        }
    }

    // ─── Internal testable core ───────────────────────────────────────────────

    /// <summary>
    /// Testable dispatch core. Takes pre-fetched VM state instead of accessing the ViewModel
    /// directly, so unit tests can drive it without a WinUI DispatcherQueue.
    /// </summary>
    /// <param name="envelope">The parsed request envelope.</param>
    /// <param name="snapshot">Pre-fetched monitor list from <c>MainViewModel.SnapshotMonitors()</c>.</param>
    /// <param name="hiddenIds">Pre-fetched hidden-ID set from <c>MainViewModel.GetHiddenMonitorIds()</c>.</param>
    /// <param name="manager">The live <see cref="IMonitorManager"/> for hardware writes.</param>
    /// <param name="profiles">Pre-loaded profiles for the <c>profiles</c> command.</param>
    /// <param name="applyProfileAsync">
    /// Delegate that applies a profile by name and returns structured outcomes, or null when the
    /// profile is not found. Receives the profile name and a <see cref="CancellationToken"/>.
    /// Maps to <c>MainViewModel.ApplyProfileWithOutcomesAsync</c> in production.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One-line JSON response string.</returns>
    internal static async Task<string> BuildResponseAsync(
        CliRequestEnvelope envelope,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hiddenIds,
        IReadOnlyList<CustomVcpValueMapping> customMappings,
        IMonitorManager manager,
        PowerDisplayProfiles profiles,
        Func<string, CancellationToken, Task<IReadOnlyList<ProfileApplyOutcome>?>> applyProfileAsync,
        CancellationToken ct)
    {
        switch (envelope.Command)
        {
            // ── list ──────────────────────────────────────────────────────────
            case CliCommandNames.List:
            {
                var result = MonitorDtoProjector.BuildListResult(snapshot, hiddenIds);
                return Serialize(result);
            }

            // ── get ───────────────────────────────────────────────────────────
            case CliCommandNames.Get:
            {
                var req = envelope.Get ?? new GetRequest();
                var (result, error) = MonitorDtoProjector.BuildGetResult(
                    snapshot,
                    hiddenIds,
                    req.MonitorNumber,
                    req.MonitorId,
                    req.SettingFilter,
                    customMappings);

                if (error is not null)
                {
                    return Serialize(error);
                }

                return Serialize(result!);
            }

            // ── set ───────────────────────────────────────────────────────────
            case CliCommandNames.Set:
            {
                if (envelope.Set is null)
                {
                    return Serialize(MakeError("set", CliErrorCodes.ArgumentError, "missing 'set' payload"));
                }

                var (result, error) = await SetCommandExecutor.ExecuteAsync(
                    manager,
                    snapshot,
                    hiddenIds,
                    envelope.Set,
                    ct).ConfigureAwait(false);

                if (error is not null)
                {
                    return Serialize(error);
                }

                return Serialize(result!);
            }

            // ── capabilities ──────────────────────────────────────────────────
            case CliCommandNames.Capabilities:
            {
                var req = envelope.Capabilities ?? new CapabilitiesRequest();
                var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(
                    snapshot,
                    hiddenIds,
                    req.MonitorNumber,
                    req.MonitorId,
                    req.SettingFilter,
                    customMappings);

                if (error is not null)
                {
                    return Serialize(error);
                }

                return Serialize(result!);
            }

            // ── profiles ──────────────────────────────────────────────────────
            case CliCommandNames.Profiles:
            {
                var result = ProfileDtoProjector.BuildProfileListResult(profiles);
                return Serialize(result);
            }

            // ── apply-profile ─────────────────────────────────────────────────
            case CliCommandNames.ApplyProfile:
            {
                var profileName = envelope.ApplyProfile?.ProfileName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    return Serialize(MakeError("apply-profile", CliErrorCodes.ArgumentError, "profile name must not be empty"));
                }

                var outcomes = await applyProfileAsync(profileName, ct).ConfigureAwait(false);

                if (outcomes is null)
                {
                    // Profile not found — return ARGUMENT_ERROR / exit code 7.
                    return Serialize(MakeError(
                        CliCommandNames.ApplyProfile,
                        CliErrorCodes.ArgumentError,
                        $"profile '{profileName}' not found",
                        "run 'powerdisplay profiles' to see available profiles"));
                }

                var applyResult = ProfileDtoProjector.BuildApplyProfileResult(profileName, outcomes);
                return Serialize(applyResult);
            }

            // ── unknown command ───────────────────────────────────────────────
            default:
                return Serialize(MakeError("unknown", CliErrorCodes.InternalError, $"unknown command '{envelope.Command}'"));
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
            return Serialize(MakeError("unknown", CliErrorCodes.InternalError, "could not parse request envelope"));
        }

        // Marshal all ViewModel/MonitorManager access onto the UI thread.
        // Serialization of the resulting string happens back on this background thread.
        return await RunOnUiThreadAsync(async () =>
        {
            // Snapshot VM state on the UI thread — these reads touch _settingsUtils and
            // _monitors which are UI-thread-owned.
            var snapshot = _vm.SnapshotMonitors();
            var hiddenIds = _vm.GetHiddenMonitorIds();
            var customMappings = _vm.CustomVcpMappings;
            var manager = _vm.MonitorManager;
            var profiles = ProfileService.LoadProfiles();

            return await BuildResponseAsync(
                envelope,
                snapshot,
                hiddenIds,
                customMappings,
                manager,
                profiles,
                (name, token) => _vm.ApplyProfileWithOutcomesAsync(name),
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

    // ─── Serialization helpers ────────────────────────────────────────────────

    /// <summary>Serializes a <see cref="CliListResult"/> to one-line JSON.</summary>
    private static string Serialize(CliListResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliListResult);

    /// <summary>Serializes a <see cref="CliGetResult"/> to one-line JSON.</summary>
    private static string Serialize(CliGetResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliGetResult);

    /// <summary>Serializes a <see cref="CliSetResult"/> to one-line JSON.</summary>
    private static string Serialize(CliSetResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliSetResult);

    /// <summary>Serializes a <see cref="CliCapabilitiesResult"/> to one-line JSON.</summary>
    private static string Serialize(CliCapabilitiesResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliCapabilitiesResult);

    /// <summary>Serializes a <see cref="CliProfileListResult"/> to one-line JSON.</summary>
    private static string Serialize(CliProfileListResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliProfileListResult);

    /// <summary>Serializes a <see cref="CliApplyProfileResult"/> to one-line JSON.</summary>
    private static string Serialize(CliApplyProfileResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliApplyProfileResult);

    /// <summary>Serializes a <see cref="CliErrorResult"/> to one-line JSON.</summary>
    private static string Serialize(CliErrorResult v)
        => JsonSerializer.Serialize(v, ContractsJsonContext.Default.CliErrorResult);

    // ─── Error factory ────────────────────────────────────────────────────────
    private static CliErrorResult MakeError(string command, string code, string message, string? hint = null)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = code,
                Message = message,
                Hint = hint,
            },
        };
}

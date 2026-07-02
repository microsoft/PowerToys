// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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
/// <b>Error contract:</b> <see cref="HandleAsync"/> never throws. Cancellation (Ctrl+C / overrun
/// of the CLI timeout) is reported as <see cref="CliErrorCodes.Timeout"/> / exit 8; any other
/// unexpected exception is reported as <see cref="CliErrorCodes.InternalError"/> / exit 9.
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
    /// A one-line JSON string. Always a valid response — never throws. Cancellation maps to
    /// <see cref="CliErrorCodes.Timeout"/>; any other unexpected exception maps to
    /// <see cref="CliErrorCodes.InternalError"/>.
    /// </returns>
    public async Task<string> HandleAsync(string requestJson, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(requestJson, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation while marshalling onto the UI thread (before the command ran). Mirror the
            // command-execution timeout contract: report TIMEOUT (exit 8), not INTERNAL_ERROR.
            var timeoutErr = MakeError("unknown", CliErrorCodes.Timeout, "request timed out or was cancelled");
            return Serialize(timeoutErr, ContractsJsonContext.Default.CliErrorResult);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[CliRequestHandler] Unexpected exception in HandleAsync: {ex.GetType().Name}: {ex.Message}");
            var errorResult = MakeCodedError("unknown", CliErrorCodes.InternalError, CliMessageIds.InternalError, detail: ex.Message);
            return Serialize(errorResult, ContractsJsonContext.Default.CliErrorResult);
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
    /// <param name="loadProfiles">
    /// Lazy profile loader, invoked only by the <c>profiles</c> command so the read commands do not
    /// pay for synchronous disk I/O they never use. Maps to <c>ProfileService.LoadProfiles</c>.
    /// </param>
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
        int defaultStep,
        Func<PowerDisplayProfiles> loadProfiles,
        Func<string, CancellationToken, Task<IReadOnlyList<ProfileApplyOutcome>?>> applyProfileAsync,
        CancellationToken ct)
    {
        try
        {
            switch (envelope.Command)
            {
                // ── list ──────────────────────────────────────────────────────────
                case CliCommandNames.List:
                {
                    var result = MonitorDtoProjector.BuildListResult(snapshot, hiddenIds);
                    return Serialize(result, ContractsJsonContext.Default.CliListResult);
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
                        return Serialize(error, ContractsJsonContext.Default.CliErrorResult);
                    }

                    return Serialize(result!, ContractsJsonContext.Default.CliGetResult);
                }

                // ── set ───────────────────────────────────────────────────────────
                case CliCommandNames.Set:
                {
                    if (envelope.Set is null)
                    {
                        return Serialize(MakeError(CliCommandNames.Set, CliErrorCodes.ArgumentError, "missing 'set' payload"), ContractsJsonContext.Default.CliErrorResult);
                    }

                    var (result, error) = await SetCommandExecutor.ExecuteAsync(
                        manager,
                        snapshot,
                        hiddenIds,
                        envelope.Set,
                        ct).ConfigureAwait(false);

                    if (error is not null)
                    {
                        return Serialize(error, ContractsJsonContext.Default.CliErrorResult);
                    }

                    return Serialize(result!, ContractsJsonContext.Default.CliSetResult);
                }

                // ── up / down (relative adjust) ─────────────────────────────────────
                case CliCommandNames.Up:
                case CliCommandNames.Down:
                {
                    if (envelope.Adjust is null)
                    {
                        return Serialize(MakeError(envelope.Command, CliErrorCodes.ArgumentError, "missing 'adjust' payload"), ContractsJsonContext.Default.CliErrorResult);
                    }

                    var (result, error) = await AdjustCommandExecutor.ExecuteAsync(
                        manager,
                        snapshot,
                        hiddenIds,
                        envelope.Adjust,
                        isUp: envelope.Command == CliCommandNames.Up,
                        defaultStep,
                        ct).ConfigureAwait(false);

                    if (error is not null)
                    {
                        return Serialize(error, ContractsJsonContext.Default.CliErrorResult);
                    }

                    return Serialize(result!, ContractsJsonContext.Default.CliSetResult);
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
                        return Serialize(error, ContractsJsonContext.Default.CliErrorResult);
                    }

                    return Serialize(result!, ContractsJsonContext.Default.CliCapabilitiesResult);
                }

                // ── profiles ──────────────────────────────────────────────────────
                case CliCommandNames.Profiles:
                {
                    var result = ProfileDtoProjector.BuildProfileListResult(loadProfiles());
                    return Serialize(result, ContractsJsonContext.Default.CliProfileListResult);
                }

                // ── apply-profile ─────────────────────────────────────────────────
                case CliCommandNames.ApplyProfile:
                {
                    var profileName = envelope.ApplyProfile?.ProfileName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(profileName))
                    {
                        return Serialize(MakeError(CliCommandNames.ApplyProfile, CliErrorCodes.ArgumentError, "profile name must not be empty"), ContractsJsonContext.Default.CliErrorResult);
                    }

                    var outcomes = await applyProfileAsync(profileName, ct).ConfigureAwait(false);

                    if (outcomes is null)
                    {
                        // Profile not found — return ARGUMENT_ERROR / exit code 7.
                        return Serialize(
                            MakeCodedError(
                                CliCommandNames.ApplyProfile,
                                CliErrorCodes.ArgumentError,
                                CliMessageIds.ProfileNotFound,
                                value: profileName),
                            ContractsJsonContext.Default.CliErrorResult);
                    }

                    var applyResult = ProfileDtoProjector.BuildApplyProfileResult(profileName, outcomes);
                    return Serialize(applyResult, ContractsJsonContext.Default.CliApplyProfileResult);
                }

                // ── unknown command ───────────────────────────────────────────────
                // A command name the app does not recognize is a bad argument (e.g. a newer CLI
                // talking to an older app), not an internal app fault — map it to ARGUMENT_ERROR
                // (exit 7) like the apply-profile not-found path, not INTERNAL_ERROR (exit 9).
                default:
                    return Serialize(MakeCodedError(envelope.Command, CliErrorCodes.ArgumentError, CliMessageIds.UnknownCommand, value: envelope.Command), ContractsJsonContext.Default.CliErrorResult);
            }
        }
        catch (OperationCanceledException)
        {
            // A blocking hardware write (set / apply-profile) overran the CLI timeout or was cancelled
            // (Ctrl+C). The partial write cannot be rolled back, so report TIMEOUT (exit 8) rather
            // than a false success — this honours the contract documented in SetCommandExecutor.
            return Serialize(
                MakeError(envelope.Command, CliErrorCodes.Timeout, "operation timed out or was cancelled"),
                ContractsJsonContext.Default.CliErrorResult);
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
            return Serialize(MakeCodedError("unknown", CliErrorCodes.InternalError, CliMessageIds.InternalError, detail: "could not parse request envelope"), ContractsJsonContext.Default.CliErrorResult);
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

            return await BuildResponseAsync(
                envelope,
                snapshot,
                hiddenIds,
                customMappings,
                manager,
                _vm.MouseWheelIncrement,
                ProfileService.LoadProfiles,
                (name, token) => _vm.ApplyProfileWithOutcomesAsync(name, token),
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

    // ─── Serialization helper ─────────────────────────────────────────────────

    /// <summary>
    /// Serializes a response DTO to one-line JSON using its source-generated
    /// <see cref="JsonTypeInfo{T}"/> (AOT/trim safe). One generic helper replaces a per-type
    /// overload set, so a new result DTO needs no new method here.
    /// </summary>
    private static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(value, typeInfo);

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

    // Code-only error: the app names the message via CliMessageIds and supplies structured data;
    // the CLI localizes the human-readable text. Value/Detail feed the localized template.
    private static CliErrorResult MakeCodedError(string command, string code, string messageId, string? value = null, string? detail = null)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = code,
                MessageId = messageId,
                Value = value,
                Detail = detail,
            },
        };
}

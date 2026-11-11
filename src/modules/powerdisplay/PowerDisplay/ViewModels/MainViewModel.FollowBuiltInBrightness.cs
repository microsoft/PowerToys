// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerDisplay.Helpers;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel: "follow built-in display brightness": mirrors the Windows built-in display
/// brightness (changed by the laptop brightness keys, Quick Settings, etc.) onto external monitors.
/// </summary>
public partial class MainViewModel
{
    // How long after a profile apply / startup restore built-in brightness changes are ignored.
    // Windows reports Power Display's own write to the built-in panel asynchronously, so the echo
    // can arrive after the apply task has completed.
    private static readonly TimeSpan BulkApplyEchoWindow = TimeSpan.FromSeconds(2);

    private DispatcherQueueTimer? _followBrightnessCommitTimer;
    private int _pendingFollowBrightness;

    // Bulk applies in progress and the end of the trailing echo window (UTC ticks). Accessed
    // atomically so the guard stays correct even if an apply is ever started off the UI thread.
    private int _bulkApplyDepth;
    private long _suppressFollowUntilTicks;

    /// <summary>
    /// Gets a value indicating whether built-in display brightness changes are mirrored to external
    /// monitors. Loaded from <c>PowerDisplayProperties.FollowBuiltInDisplayBrightness</c>; off by default.
    /// </summary>
    internal bool FollowBuiltInDisplayBrightness { get; private set; }

    /// <summary>
    /// Returns true for monitors that follow the built-in display: external (non-WMI) monitors that
    /// support brightness and are not excluded from sync. The built-in panel itself is skipped because
    /// Windows already applied the value to it, and writing it back would only re-trigger the watcher.
    /// </summary>
    private bool IsFollowTarget(MonitorViewModel monitor) =>
        !monitor.IsInternal && monitor.SupportsBrightness && !_excludedMonitorIds.Contains(monitor.Id);

    /// <summary>
    /// Handles a built-in brightness change (dispatcher thread). Updates the UI immediately and
    /// schedules one debounced DDC/CI write per target, so holding a brightness key (about one
    /// change every 100 ms) results in a single trailing write instead of a burst on the I2C bus.
    /// </summary>
    private void OnBuiltInBrightnessChanged(object? sender, int value)
    {
        if (!FollowBuiltInDisplayBrightness)
        {
            return;
        }

        // Monitors are being re-enumerated; the next key press after discovery will apply.
        if (IsScanning)
        {
            return;
        }

        // A profile or startup restore gives every monitor its own explicit value, including the
        // built-in panel. Mirroring the panel's echo would overwrite the externals' saved values.
        if (Volatile.Read(ref _bulkApplyDepth) > 0 || _clock.UtcNow.Ticks < Interlocked.Read(ref _suppressFollowUntilTicks))
        {
            Logger.LogInfo($"[FollowBuiltInBrightness] Ignoring {value}% during profile/restore apply");
            return;
        }

        _pendingFollowBrightness = value;

        foreach (var vm in Monitors)
        {
            if (IsFollowTarget(vm))
            {
                vm.UpdateBrightnessDisplay(value);
            }
            else if (vm.IsInternal && vm.SupportsBrightness)
            {
                // Windows already applied the value to the panel; just keep its card in sync.
                vm.UpdateBrightnessDisplay(value);
            }
        }

        // Keep the "All Displays" slider in step without triggering its own broadcast.
        if (LinkedLevelsActive)
        {
            SetLinkedBrightnessSilently(value);
        }

        SliderCommitScheduler.Schedule(
            ref _followBrightnessCommitTimer,
            _dispatcherQueue,
            () => ApplyFollowBrightnessAsync(_pendingFollowBrightness));
    }

    /// <summary>
    /// Marks a bulk apply (profile, startup restore) for its lifetime plus
    /// <see cref="BulkApplyEchoWindow"/>, so built-in brightness echoes it causes are not mirrored.
    /// Use with <c>using</c> at the top of the apply method.
    /// </summary>
    private BulkApplyScope SuppressFollowForBulkApply()
    {
        Interlocked.Increment(ref _bulkApplyDepth);
        _followBrightnessCommitTimer?.Stop();
        return new BulkApplyScope(this);
    }

    private async Task ApplyFollowBrightnessAsync(int value)
    {
        if (!FollowBuiltInDisplayBrightness)
        {
            return;
        }

        try
        {
            var writes = Monitors
                .Where(IsFollowTarget)
                .Select(m => m.SetBrightnessAsync(value))
                .ToList();

            if (writes.Count > 0)
            {
                Logger.LogInfo($"[FollowBuiltInBrightness] Applying {value}% to {writes.Count} monitor(s)");
                await Task.WhenAll(writes);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FollowBuiltInBrightness] Apply failed: {ex.Message}");
        }
    }

    private readonly struct BulkApplyScope : IDisposable
    {
        private readonly MainViewModel _owner;

        public BulkApplyScope(MainViewModel owner) => _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner._suppressFollowUntilTicks, (_owner._clock.UtcNow + BulkApplyEchoWindow).Ticks);
            Interlocked.Decrement(ref _owner._bulkApplyDepth);
        }
    }
}

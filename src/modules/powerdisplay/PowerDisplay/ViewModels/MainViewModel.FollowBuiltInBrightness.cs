// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel: "follow built-in display brightness": mirrors the Windows built-in display
/// brightness (changed by the laptop brightness keys, Quick Settings, etc.) onto external monitors.
/// </summary>
/// <remarks>
/// Follow does not own a hardware write path. It feeds the same debounced commits a user gesture
/// uses (the per-display brightness timer, or the linked broadcast while link mode is on), so each
/// display has at most one pending write and the latest input, from Windows or from a slider, wins.
/// </remarks>
public partial class MainViewModel
{
    // How long an expected echo of Power Display's own built-in brightness write stays valid.
    // Windows reports the write asynchronously, so the echo can arrive after the apply completed.
    private const long BuiltInWriteEchoWindowMs = 2000;

    private bool _followBuiltInDisplayBrightness;

    // Bulk applies (profile, startup restore) in progress. Accessed atomically so the guard stays
    // correct even if an apply is ever started off the UI thread.
    private int _bulkApplyDepth;

    // Built-in brightness value written by a bulk apply whose Windows echo has not arrived yet
    // (-1 = none), and its expiry on the monotonic Environment.TickCount64 clock, so wall-clock
    // adjustments cannot stretch or shrink the window.
    private int _expectedBuiltInEcho = -1;
    private long _expectedBuiltInEchoUntilMs;

    /// <summary>
    /// Gets a value indicating whether built-in display brightness changes are mirrored to external
    /// monitors. Loaded from <c>PowerDisplayProperties.FollowBuiltInDisplayBrightness</c>; off by default.
    /// </summary>
    internal bool FollowBuiltInDisplayBrightness
    {
        get => _followBuiltInDisplayBrightness;
        private set => SetProperty(ref _followBuiltInDisplayBrightness, value);
    }

    /// <summary>
    /// Returns true for monitors that follow the built-in display: external (non-WMI) monitors that
    /// support brightness and are not excluded from sync. The built-in panel itself is skipped because
    /// Windows already applied the value to it, and writing it back would only re-trigger the watcher.
    /// </summary>
    internal bool IsFollowTarget(MonitorViewModel monitor) =>
        !monitor.IsInternal && monitor.SupportsBrightness && !_excludedMonitorIds.Contains(monitor.Id);

    /// <summary>
    /// Returns true when <paramref name="monitor"/> is the built-in panel and Windows already reports
    /// <paramref name="value"/> for it. While follow is on, the linked broadcast skips that redundant
    /// write so a value that came from Windows is not written back to the panel.
    /// </summary>
    private bool IsBuiltInAlreadyAt(MonitorViewModel monitor, int value) =>
        FollowBuiltInDisplayBrightness && monitor.IsInternal && value == _builtInBrightnessWatcher.LastBrightness;

    /// <summary>
    /// Gets a value indicating whether a queued follow write may still reach hardware. Checked by
    /// <see cref="MonitorViewModel"/> when its debounce timer fires, because the setting, the
    /// exclusion set or the scanning state can change between scheduling and commit.
    /// </summary>
    internal bool CanCommitFollowBrightness(MonitorViewModel monitor) =>
        FollowBuiltInDisplayBrightness && !IsScanning && IsFollowTarget(monitor);

    /// <summary>
    /// Handles a built-in brightness change (dispatcher thread). Updates the cards immediately and
    /// feeds the value into the existing debounced commits, so holding a brightness key (about one
    /// change every 100 ms) results in a single trailing DDC/CI write per display.
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

        // A profile or startup restore wrote this value to the built-in panel itself and gave every
        // other monitor its own explicit value. Mirroring that echo would overwrite them.
        if (ConsumeExpectedBuiltInEcho(value))
        {
            Logger.LogInfo($"[FollowBuiltInBrightness] Ignoring echo of {value}% written by profile/restore");
            return;
        }

        foreach (var vm in Monitors)
        {
            if (vm.IsInternal && vm.SupportsBrightness)
            {
                // Windows already applied the value to the panel; keep its card (and the cached model
                // value the card is rebuilt from) in sync without writing it back.
                vm.SyncObservedBrightness(value);
            }
        }

        if (LinkedLevelsActive)
        {
            // Drive the master slider through its normal path: it updates every linked card and
            // reschedules the single linked broadcast, which coalesces with any pending slider gesture.
            // The broadcast skips the built-in panel when it already holds the value.
            LinkedBrightness = value;
            return;
        }

        foreach (var vm in Monitors)
        {
            if (IsFollowTarget(vm))
            {
                vm.ScheduleFollowBrightness(value);
            }
        }
    }

    /// <summary>
    /// Called by <see cref="MonitorViewModel.SetBrightnessAsync"/> before it writes the built-in
    /// panel. During a bulk apply, remembers the value so its Windows echo is not mirrored to the
    /// external monitors. Writes outside bulk applies (the built-in card's slider) are followed as
    /// usual, and a write of the value Windows already reports produces no echo, so it is not tracked.
    /// </summary>
    internal void OnBuiltInBrightnessWriting(int value)
    {
        if (Volatile.Read(ref _bulkApplyDepth) == 0 || value == _builtInBrightnessWatcher.LastBrightness)
        {
            return;
        }

        Interlocked.Exchange(ref _expectedBuiltInEchoUntilMs, Environment.TickCount64 + BuiltInWriteEchoWindowMs);
        Interlocked.Exchange(ref _expectedBuiltInEcho, value);
    }

    /// <summary>
    /// Returns true when <paramref name="value"/> is the pending echo of a bulk-apply write. The
    /// expectation is cleared by the first notification after the write (or when it expires), so a
    /// later real change to the same value is followed normally.
    /// </summary>
    private bool ConsumeExpectedBuiltInEcho(int value)
    {
        int expected = Interlocked.Exchange(ref _expectedBuiltInEcho, -1);
        return expected >= 0
            && expected == value
            && Environment.TickCount64 < Interlocked.Read(ref _expectedBuiltInEchoUntilMs);
    }

    /// <summary>
    /// Drops follow writes that are still waiting for their debounce timer and restores each
    /// affected card to the value the hardware last confirmed. Used when a rescan or a profile is
    /// about to replace the monitor set or its values.
    /// </summary>
    private void CancelPendingFollowBrightness()
    {
        foreach (var vm in Monitors)
        {
            vm.CancelPendingFollowBrightness();
        }
    }

    /// <summary>
    /// Marks a bulk apply (profile, startup restore) for its lifetime, so the built-in brightness it
    /// writes is recognised as its own echo, and drops queued brightness writes it supersedes. Use with <c>using</c> at the top of the apply method.
    /// </summary>
    private BulkApplyScope BeginBulkApply()
    {
        Interlocked.Increment(ref _bulkApplyDepth);
        CancelPendingFollowBrightness();

        // A linked broadcast still waiting for its timer would never run (profiles turn link mode
        // off); put the linked cards back on their confirmed values instead of an unsent preview.
        if (_linkedBrightnessCommitTimer?.IsRunning == true)
        {
            CancelPendingLinkedBrightnessCommit();
            foreach (var vm in Monitors)
            {
                if (IsLinkedTarget(vm))
                {
                    vm.RevertBrightnessPreview();
                }
            }
        }

        return new BulkApplyScope(this);
    }

    private readonly struct BulkApplyScope : IDisposable
    {
        private readonly MainViewModel _owner;

        public BulkApplyScope(MainViewModel owner) => _owner = owner;

        public void Dispose() => Interlocked.Decrement(ref _owner._bulkApplyDepth);
    }
}

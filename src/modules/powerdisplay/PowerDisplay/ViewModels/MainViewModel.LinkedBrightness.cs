// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerDisplay.Common.Services;
using PowerDisplay.Helpers;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel — linked-brightness behavior: toggle side effects, initial value,
/// per-monitor exclusion set, debounced broadcast across linked monitors.
/// </summary>
public partial class MainViewModel
{
    // Monitor Ids (Monitor.Id form) the user has excluded from linked brightness. An excluded
    // monitor keeps its own slider while link mode is on. Authoritative runtime copy of
    // PowerDisplayProperties.ExcludedFromSyncMonitorIds; loaded by LoadExcludedMonitorIds and
    // persisted by SaveExcludedMonitorIds. Monitors not in this set are linked by default.
    private readonly HashSet<string> _excludedMonitorIds = new(StringComparer.OrdinalIgnoreCase);

    // Set while the toggle handler is seeding the initial LinkedBrightness value
    // so OnLinkedBrightnessChanged does not treat the seed as a user-driven change
    // and schedule a broadcast — the design promise is "no auto-write on toggle ON".
    private bool _suppressLinkedBrightnessBroadcast;

    private DispatcherQueueTimer? _linkedBrightnessCommitTimer;

    /// <summary>
    /// Returns true when the monitor is currently driven by linked brightness — it supports
    /// brightness and the user has not excluded it. This mirrors the planner's linked-target rule
    /// for live ViewModels on the broadcast and optimistic-update hot paths.
    /// </summary>
    private bool IsLinkedTarget(MonitorViewModel monitor) =>
        monitor.SupportsBrightness && !_excludedMonitorIds.Contains(monitor.Id);

    /// <summary>
    /// Snapshot the currently included brightness-capable monitors as
    /// <see cref="LinkedBrightnessPlanner.LinkTarget"/> values for seed decisions.
    /// </summary>
    private List<LinkedBrightnessPlanner.LinkTarget> BuildLinkTargets() =>
        Monitors.Where(IsLinkedTarget)
            .Select(m => new LinkedBrightnessPlanner.LinkTarget(
            m.Id,
            m.MonitorNumber,
            m.Brightness))
            .ToList();

    /// <summary>
    /// Gets whether the given monitor Id is excluded from linked brightness. Queried by
    /// <see cref="MonitorViewModel.IsExcludedFromSync"/> so each card reflects the shared set.
    /// </summary>
    internal bool IsMonitorExcludedFromSync(string monitorId) => _excludedMonitorIds.Contains(monitorId);

    /// <summary>
    /// Add or remove a monitor from the linked-brightness exclusion set, persist the change, and
    /// refresh the "N linked" summary. When a monitor is brought back into an existing linked set
    /// (<paramref name="excluded"/> = false) while link mode is on, re-broadcast so it snaps to
    /// the current master level. If it is the first linked target, seed the master slider from it
    /// instead. Excluding a monitor does not write hardware: it simply stops following the group.
    /// </summary>
    internal void SetMonitorExcludedFromSync(string monitorId, bool excluded)
    {
        var hadLinkedTarget = Monitors.Any(IsLinkedTarget);
        var changed = excluded
            ? _excludedMonitorIds.Add(monitorId)
            : _excludedMonitorIds.Remove(monitorId);

        if (!changed)
        {
            return;
        }

        SaveExcludedMonitorIds();

        // Excluding can empty the linked set (slider must disable); including can refill it.
        IsLinkedBrightnessAvailable = Monitors.Any(IsLinkedTarget);
        OnPropertyChanged(nameof(LinkedMonitorsCount));
        OnPropertyChanged(nameof(LinkedMonitorsCountText));
        OnPropertyChanged(nameof(ExcludedMonitorsCount));
        OnPropertyChanged(nameof(ExcludedMonitorsCountText));
        OnPropertyChanged(nameof(HasExcludedMonitors));

        if (!excluded && LinkedLevelsActive)
        {
            if (hadLinkedTarget)
            {
                ScheduleLinkedBrightnessCommit();
            }
            else
            {
                SeedInitialLinkedBrightness();
            }
        }
    }

    /// <summary>
    /// Replace the runtime exclusion set from persisted settings. Called by LoadUIDisplaySettings
    /// before monitor discovery so cards read the correct excluded state on first render.
    /// </summary>
    internal void LoadExcludedMonitorIds(IEnumerable<string>? excludedIds)
    {
        _excludedMonitorIds.Clear();
        if (excludedIds == null)
        {
            return;
        }

        foreach (var id in excludedIds)
        {
            if (!string.IsNullOrEmpty(id))
            {
                _excludedMonitorIds.Add(id);
            }
        }
    }

    /// <summary>
    /// Partial change hook fired by the source-generator for <see cref="LinkedLevelsActive"/>.
    /// Persists the new value to settings.json and, on transition to ON, seeds the
    /// linked-brightness slider with an initial value drawn from the connected monitors.
    /// </summary>
    partial void OnLinkedLevelsActiveChanged(bool value)
    {
        SaveLinkedLevelsActive(value);

        if (value)
        {
            // Entering linked mode starts with the individual cards collapsed so the master
            // slider is the focus; the user can expand to reach per-display settings.
            IndividualDisplaysExpanded = false;
            SeedInitialLinkedBrightness();
        }
        else
        {
            // Drop any pending master-slider write. Once the user turns linked mode off, the
            // delayed broadcast from the previous linked slider gesture should not still run.
            _linkedBrightnessCommitTimer?.Stop();
        }
    }

    /// <summary>
    /// Partial change hook fired by the source-generator for <see cref="LinkedBrightness"/>.
    /// Optimistically updates every linked monitor's brightness value to match the master value
    /// that will be broadcast to hardware, so it is correct if the monitor is later excluded or
    /// link mode is disabled. Then schedules a debounced hardware broadcast. Suppressed during
    /// initial-value seeding so turning the toggle on does not auto-write hardware.
    /// </summary>
    partial void OnLinkedBrightnessChanged(int value)
    {
        if (_suppressLinkedBrightnessBroadcast)
        {
            return;
        }

        if (!LinkedLevelsActive)
        {
            return;
        }

        // Keep linked monitor VMs aligned with the master value without per-VM commits. The row is
        // usually covered by the linked-mode hint, but the backing value should be correct if the
        // monitor is later excluded or link mode is disabled.
        foreach (var vm in Monitors)
        {
            if (IsLinkedTarget(vm))
            {
                vm.UpdateBrightnessDisplay(value);
            }
        }

        ScheduleLinkedBrightnessCommit();
    }

    /// <summary>
    /// Seeds <see cref="LinkedBrightness"/> with the initial value used to position the master
    /// slider when link mode turns on, via <see cref="LinkedBrightnessPlanner.Seed"/> (lowest
    /// DISPLAY number, then deterministic fallback). The seed is positional only: it is never
    /// written to hardware (the suppress flag gates <see cref="OnLinkedBrightnessChanged"/>), so
    /// the first user gesture is the first broadcast.
    /// </summary>
    private void SeedInitialLinkedBrightness()
    {
        var seed = LinkedBrightnessPlanner.Seed(BuildLinkTargets());
        IsLinkedBrightnessAvailable = seed.HasValue;
        if (seed.HasValue)
        {
            SetLinkedBrightnessSilently(seed.Value);
        }
    }

    /// <summary>
    /// Set <see cref="LinkedBrightness"/> without triggering a hardware broadcast — used for
    /// positioning the master slider during seeding.
    /// </summary>
    private void SetLinkedBrightnessSilently(int value)
    {
        _suppressLinkedBrightnessBroadcast = true;
        try
        {
            LinkedBrightness = value;
        }
        finally
        {
            _suppressLinkedBrightnessBroadcast = false;
        }
    }

    /// <summary>
    /// Recompute <see cref="IsLinkedBrightnessAvailable"/> and the linked-monitors count after a
    /// monitor add/remove/refresh. Called from <c>UpdateMonitorList</c> so the master slider's
    /// enabled state and "N linked" subtitle stay in sync with the live monitor set.
    /// </summary>
    internal void RecomputeLinkedBrightnessAvailability()
    {
        IsLinkedBrightnessAvailable = Monitors.Any(IsLinkedTarget);
        OnPropertyChanged(nameof(LinkedMonitorsCount));
        OnPropertyChanged(nameof(LinkedMonitorsCountText));
        OnPropertyChanged(nameof(ExcludedMonitorsCount));
        OnPropertyChanged(nameof(ExcludedMonitorsCountText));
        OnPropertyChanged(nameof(HasExcludedMonitors));

        if (LinkedLevelsActive)
        {
            SeedInitialLinkedBrightness();
        }
    }

    private void ScheduleLinkedBrightnessCommit()
    {
        SliderCommitScheduler.Schedule(
            ref _linkedBrightnessCommitTimer,
            _dispatcherQueue,
            () => BroadcastLinkedBrightnessAsync(LinkedBrightness));
    }

    /// <summary>
    /// Drop a queued linked-brightness hardware write. Used when the monitor topology is changing,
    /// so a delayed commit from the previous topology cannot apply to a newly discovered target set.
    /// </summary>
    private void CancelPendingLinkedBrightnessCommit()
    {
        _linkedBrightnessCommitTimer?.Stop();
    }

    /// <summary>
    /// Broadcast the linked brightness value to every linked target via
    /// <see cref="MonitorViewModel.SetBrightnessAsync"/>. Each per-VM call already wraps hardware
    /// errors in its own try/catch, so a single failing monitor does not break the others.
    /// </summary>
    private async Task BroadcastLinkedBrightnessAsync(int value)
    {
        if (!LinkedLevelsActive)
        {
            return;
        }

        try
        {
            var writes = Monitors
                .Where(IsLinkedTarget)
                .Select(m => m.SetBrightnessAsync(value))
                .ToList();

            if (writes.Count > 0)
            {
                await Task.WhenAll(writes);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[LinkedBrightness] Broadcast failed: {ex.Message}");
        }
    }
}

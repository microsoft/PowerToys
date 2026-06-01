// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Services;
using PowerDisplay.Configuration;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel — linked-brightness behavior: toggle side effects, initial-value
/// fallback chain, per-monitor exclusion set, debounced broadcast across linked monitors.
/// </summary>
public partial class MainViewModel
{
    // Monitor Ids (Monitor.Id form) the user has excluded from linked brightness. An excluded
    // monitor keeps its own slider while link mode is on. Authoritative runtime copy of
    // PowerDisplayProperties.ExcludedFromSyncMonitorIds; loaded by LoadExcludedMonitorIds and
    // persisted by SaveExcludedMonitorIds. Monitors not in this set are linked by default.
    private readonly HashSet<string> _excludedMonitorIds = new(StringComparer.OrdinalIgnoreCase);

    // Set while LoadUIDisplaySettings is pushing the on-disk value into the
    // LinkedLevelsActive observable property, so the partial-generated change
    // hook does not re-save the value we just loaded.
    private bool _suppressLinkedLevelsActiveSave;

    // Set while the toggle handler is seeding the initial LinkedBrightness value
    // so OnLinkedBrightnessChanged does not treat the seed as a user-driven change
    // and schedule a broadcast — the design promise is "no auto-write on toggle ON".
    private bool _suppressLinkedBrightnessBroadcast;

    // True when link mode is active but the initial slider value could not be seeded yet because
    // no linked target with a readable brightness was available (e.g. link was persisted ON and the
    // toggle hook ran during construction, before monitor discovery; every monitor is currently
    // excluded; or hardware reads failed). The seed is applied as soon as a readable target appears
    // (see TrySeedPendingInitialBrightness), so a restart with link enabled does not leave the
    // master slider stuck at its default 0.
    private bool _pendingInitialSeed;

    private DispatcherQueueTimer? _linkedBrightnessCommitTimer;

    /// <summary>
    /// Returns true when the monitor is currently driven by linked brightness — it supports
    /// brightness and the user has not excluded it. The live-VM counterpart of
    /// <see cref="LinkedBrightnessPlanner.IsLinkedTarget"/> (same rule), used on the broadcast and
    /// optimistic-update hot paths to avoid allocating planner structs per slider tick.
    /// </summary>
    private bool IsLinkedTarget(MonitorViewModel monitor) =>
        monitor.SupportsBrightness && !_excludedMonitorIds.Contains(monitor.Id);

    /// <summary>
    /// Snapshot the current monitors as <see cref="LinkedBrightnessPlanner.LinkTarget"/> values for
    /// the pure planner (seed / availability decisions).
    /// </summary>
    private List<LinkedBrightnessPlanner.LinkTarget> BuildLinkTargets()
    {
        // Switching the Windows primary display does not necessarily trigger monitor rediscovery.
        // Query it again when planning so the next linked-mode seed follows the current primary.
        var primaryGdiDeviceName = DisplayConfigInventory.GetPrimaryGdiDeviceName();

        return Monitors.Select(m => new LinkedBrightnessPlanner.LinkTarget(
            m.Id,
            m.MonitorNumber,
            m.Brightness,
            m.SupportsBrightness,
            _excludedMonitorIds.Contains(m.Id),
            LinkedBrightnessPlanner.ResolveIsPrimary(m.GdiDeviceName, primaryGdiDeviceName),
            m.HasValidBrightness))
            .ToList();
    }

    /// <summary>
    /// Gets whether the given monitor Id is excluded from linked brightness. Queried by
    /// <see cref="MonitorViewModel.IsExcludedFromSync"/> so each card reflects the shared set.
    /// </summary>
    internal bool IsMonitorExcludedFromSync(string monitorId) => _excludedMonitorIds.Contains(monitorId);

    /// <summary>
    /// Add or remove a monitor from the linked-brightness exclusion set, persist the change, and
    /// refresh the "N linked" summary. When a monitor is brought back into the linked set
    /// (<paramref name="excluded"/> = false) while link mode is on, re-broadcast so it snaps to
    /// the current master level — that is the user's explicit intent in clicking "link this one".
    /// Excluding a monitor does not write hardware: it simply stops following the group.
    /// </summary>
    internal void SetMonitorExcludedFromSync(string monitorId, bool excluded)
    {
        var changed = excluded
            ? _excludedMonitorIds.Add(monitorId)
            : _excludedMonitorIds.Remove(monitorId);

        if (!changed)
        {
            return;
        }

        SaveExcludedMonitorIds();

        // Excluding can empty the linked set (slider must disable); including can refill it (slider
        // re-enables, and a deferred startup seed may now be possible).
        IsLinkedBrightnessAvailable = !_pendingInitialSeed && Monitors.Any(IsLinkedTarget);
        OnPropertyChanged(nameof(LinkedMonitorsCount));
        OnPropertyChanged(nameof(LinkedMonitorsCountText));
        OnPropertyChanged(nameof(ExcludedMonitorsCount));
        OnPropertyChanged(nameof(ExcludedMonitorsCountText));
        OnPropertyChanged(nameof(HasExcludedMonitors));
        TrySeedPendingInitialBrightness();

        if (!excluded && LinkedLevelsActive)
        {
            ScheduleLinkedBrightnessCommit();
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
        if (!_suppressLinkedLevelsActiveSave)
        {
            SaveLinkedLevelsActive(value);
        }

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
    /// slider when link mode turns on, via <see cref="LinkedBrightnessPlanner.Seed"/> (Windows
    /// primary display, then deterministic fallback). When no linked target has a readable
    /// brightness yet — link was persisted ON and this ran during construction before discovery,
    /// every monitor is excluded, or hardware reads failed — the seed is deferred
    /// (<see cref="_pendingInitialSeed"/>) and applied the moment a readable target appears, so a
    /// restart with link enabled never leaves the slider stuck at its default 0. The seed is
    /// positional only: it is never written to hardware (the suppress flag gates
    /// <see cref="OnLinkedBrightnessChanged"/>), so the first user gesture is the first broadcast.
    /// </summary>
    private void SeedInitialLinkedBrightness()
    {
        var seed = LinkedBrightnessPlanner.Seed(BuildLinkTargets());
        if (!seed.HasValue)
        {
            // No readable target yet — defer until one appears (post-discovery or after un-excluding).
            _pendingInitialSeed = true;
            IsLinkedBrightnessAvailable = false;
            return;
        }

        _pendingInitialSeed = false;
        IsLinkedBrightnessAvailable = true;
        SetLinkedBrightnessSilently(seed.Value);
    }

    /// <summary>
    /// Apply the deferred initial seed once link mode is active and a linked target with a readable
    /// brightness has become available. No-op otherwise. Safe to call after every discovery,
    /// exclusion change, or successful brightness write.
    /// </summary>
    internal void TrySeedPendingInitialBrightness()
    {
        if (!_pendingInitialSeed || !LinkedLevelsActive)
        {
            return;
        }

        var seed = LinkedBrightnessPlanner.Seed(BuildLinkTargets());
        if (seed.HasValue)
        {
            _pendingInitialSeed = false;
            IsLinkedBrightnessAvailable = true;
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
    /// enabled state and "N linked" subtitle stay in sync with the live monitor set, and applies a
    /// deferred startup seed once monitors have been discovered.
    /// </summary>
    internal void RecomputeLinkedBrightnessAvailability()
    {
        IsLinkedBrightnessAvailable = !_pendingInitialSeed && Monitors.Any(IsLinkedTarget);
        OnPropertyChanged(nameof(LinkedMonitorsCount));
        OnPropertyChanged(nameof(LinkedMonitorsCountText));
        OnPropertyChanged(nameof(ExcludedMonitorsCount));
        OnPropertyChanged(nameof(ExcludedMonitorsCountText));
        OnPropertyChanged(nameof(HasExcludedMonitors));
        TrySeedPendingInitialBrightness();
    }

    private void ScheduleLinkedBrightnessCommit()
    {
        if (_linkedBrightnessCommitTimer == null)
        {
            _linkedBrightnessCommitTimer = _dispatcherQueue.CreateTimer();
            _linkedBrightnessCommitTimer.IsRepeating = false;
            _linkedBrightnessCommitTimer.Interval = TimeSpan.FromMilliseconds(AppConstants.UI.SliderCommitDebounceMs);
            _linkedBrightnessCommitTimer.Tick += (_, _) => _ = BroadcastLinkedBrightnessAsync(LinkedBrightness);
        }

        _linkedBrightnessCommitTimer.Stop();
        _linkedBrightnessCommitTimer.Start();
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

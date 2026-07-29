// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using MouseWheelMode = PowerDisplay.Models.MouseWheelControlMode;

namespace PowerDisplay.ViewModels;

public partial class MainViewModel
{
    private const uint MonitorDefaultToPrimary = 1;

    private bool _trayWheelNoTargetLogged;

    /// <summary>
    /// Gets a value indicating whether a wheel notch delivered right now would produce a brightness
    /// change. The tray hook arms only while this holds, so it never consumes a notch that no
    /// monitor can accept. The tray service re-reads this for every hover message, so it answers
    /// the question by scanning for the first eligible monitor instead of planning the full set.
    /// </summary>
    public bool CanAdjustBrightnessFromTrayWheel
    {
        get
        {
            var mode = MouseWheelControlMode.Normalize();
            if (!TryGetTrayWheelScope(mode, out var primaryGdiDeviceName))
            {
                return false;
            }

            // Indexed rather than foreach: ObservableCollection hands out a boxed enumerator, and
            // this runs on every tray hover message.
            for (var i = 0; i < Monitors.Count; i++)
            {
                if (TrayWheelAdjustmentPlanner.IsEligible(
                    mode,
                    CreateTrayWheelTarget(Monitors[i]),
                    primaryGdiDeviceName))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Applies complete tray wheel notches to the configured brightness targets. The brightness
    /// change is its own feedback, so nothing is reported back to the caller.
    /// </summary>
    /// <param name="notches">The signed number of complete wheel notches.</param>
    public void AdjustBrightnessFromTrayWheel(int notches)
    {
        var mode = MouseWheelControlMode.Normalize();
        var adjustments = PlanTrayWheelAdjustments(mode, notches);

        if (adjustments.Count == 0)
        {
            if (!_trayWheelNoTargetLogged)
            {
                Logger.LogWarning("[TrayWheel] No valid brightness target was available");
                _trayWheelNoTargetLogged = true;
            }

            return;
        }

        _trayWheelNoTargetLogged = false;

        // Linked monitors are driven by the master value, not their own setter: a per-VM commit
        // would leave the master slider stale and the next broadcast would revert the wheel
        // adjustment. Excluded monitors keep their own value and are adjusted individually.
        var linkedBrightness = 0;
        var hasLinkedTarget = false;

        foreach (var adjustment in adjustments)
        {
            foreach (var monitor in Monitors)
            {
                if (!MonitorIdComparer.Equal(monitor.Id, adjustment.Id))
                {
                    continue;
                }

                if (LinkedLevelsActive && IsLinkedTarget(monitor))
                {
                    // The group moves as one, so only the first linked target sets the master.
                    // Step from the planner's value for this monitor rather than from
                    // LinkedBrightness: the master is positional only - SeedInitialLinkedBrightness
                    // takes it from the lowest-numbered linked monitor and never writes hardware,
                    // and every monitor-list rebuild re-seeds it - so it can sit arbitrarily far
                    // from the monitor the wheel actually named. Stepping the master relative to
                    // itself turns that drift into a wrong-sized or wrong-signed change, and a
                    // master already clamped at 0/100 would swallow the notch while writing
                    // nothing at all. The planner already clamped this value against the target's
                    // real brightness, so the group snaps to the target's level and the gesture
                    // always moves that monitor in the direction the user scrolled.
                    if (!hasLinkedTarget)
                    {
                        linkedBrightness = adjustment.Brightness;
                        hasLinkedTarget = true;
                    }
                }
                else
                {
                    monitor.Brightness = adjustment.Brightness;
                }

                break;
            }
        }

        if (hasLinkedTarget)
        {
            LinkedBrightness = linkedBrightness;
        }
    }

    private IReadOnlyList<TrayWheelAdjustmentPlanner.Adjustment> PlanTrayWheelAdjustments(
        MouseWheelMode mode,
        int notches)
    {
        if (!TryGetTrayWheelScope(mode, out var primaryGdiDeviceName))
        {
            return [];
        }

        var targets = new List<TrayWheelAdjustmentPlanner.Target>(Monitors.Count);
        foreach (var monitor in Monitors)
        {
            targets.Add(CreateTrayWheelTarget(monitor));
        }

        return TrayWheelAdjustmentPlanner.Plan(
            mode,
            targets,
            primaryGdiDeviceName,
            (long)notches * MouseWheelIncrement);
    }

    /// <summary>
    /// Validates the preconditions shared by <see cref="CanAdjustBrightnessFromTrayWheel"/> and
    /// <see cref="PlanTrayWheelAdjustments"/>, and resolves the primary display's GDI name for the
    /// modes that need it.
    /// </summary>
    /// <param name="mode">The normalized mouse-wheel mode.</param>
    /// <param name="primaryGdiDeviceName">The resolved primary GDI name, or <see langword="null"/>
    /// when the mode does not target the primary display.</param>
    /// <returns><see langword="true"/> when a tray wheel adjustment is possible in principle.</returns>
    private bool TryGetTrayWheelScope(
        MouseWheelMode mode,
        out string? primaryGdiDeviceName)
    {
        primaryGdiDeviceName = null;

        if (mode == MouseWheelMode.Disabled ||
            MouseWheelIncrement <= 0 ||
            !IsInitialized ||
            !IsInteractionEnabled)
        {
            return false;
        }

        if (mode != MouseWheelMode.PrimaryDisplay)
        {
            return true;
        }

        primaryGdiDeviceName = GetPrimaryGdiDeviceName();
        return !string.IsNullOrWhiteSpace(primaryGdiDeviceName);
    }

    private static TrayWheelAdjustmentPlanner.Target CreateTrayWheelTarget(MonitorViewModel monitor)
        => new(
            monitor.Id,
            monitor.GdiDeviceName,
            monitor.SupportsBrightness,
            monitor.HasValidBrightnessReading,
            monitor.Brightness);

    private static unsafe string? GetPrimaryGdiDeviceName()
    {
        var monitor = MonitorFromPointNative(
            new NativePoint(0, 0),
            MonitorDefaultToPrimary);
        if (monitor == 0)
        {
            return null;
        }

        var monitorInfo = new MonitorInfoEx
        {
            CbSize = (uint)sizeof(MonitorInfoEx),
        };

        return GetMonitorInfo(monitor, ref monitorInfo)
            ? monitorInfo.GetDeviceName()
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public readonly int X;

        public readonly int Y;
    }

    [LibraryImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    private static partial nint MonitorFromPointNative(
        NativePoint point,
        uint flags);
}

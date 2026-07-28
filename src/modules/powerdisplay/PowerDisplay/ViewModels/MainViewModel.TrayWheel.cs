// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            if (!TryGetTrayWheelScope(mode, notches: 1, out var primaryGdiDeviceName))
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
    /// Applies complete tray wheel notches to the configured brightness targets.
    /// </summary>
    /// <param name="notches">The signed number of complete wheel notches.</param>
    public TrayWheelAdjustmentFeedback? AdjustBrightnessFromTrayWheel(int notches)
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

            return null;
        }

        _trayWheelNoTargetLogged = false;

        // Linked monitors are driven by the master value, not their own setter: a per-VM commit
        // would leave the master slider stale and the next broadcast would revert the wheel
        // adjustment. Excluded monitors keep their own value and are adjusted individually.
        var linkedBrightness = (int)Math.Clamp(
            LinkedBrightness + ((long)notches * MouseWheelIncrement),
            0,
            100);
        var hasLinkedTarget = false;

        var brightnessValues = new List<int>(adjustments.Count);
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
                    // The group moves as one, so it contributes a single value to the readout.
                    if (!hasLinkedTarget)
                    {
                        brightnessValues.Add(linkedBrightness);
                        hasLinkedTarget = true;
                    }
                }
                else
                {
                    monitor.Brightness = adjustment.Brightness;
                    brightnessValues.Add(adjustment.Brightness);
                }

                break;
            }
        }

        if (hasLinkedTarget)
        {
            LinkedBrightness = linkedBrightness;
        }

        return new TrayWheelAdjustmentFeedback(mode, brightnessValues, hasLinkedTarget);
    }

    private IReadOnlyList<TrayWheelAdjustmentPlanner.Adjustment> PlanTrayWheelAdjustments(
        MouseWheelMode mode,
        int notches)
    {
        if (!TryGetTrayWheelScope(mode, notches, out var primaryGdiDeviceName))
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
    /// <param name="notches">The signed number of complete wheel notches.</param>
    /// <param name="primaryGdiDeviceName">The resolved primary GDI name, or <see langword="null"/>
    /// when the mode does not target the primary display.</param>
    /// <returns><see langword="true"/> when a tray wheel adjustment is possible in principle.</returns>
    private bool TryGetTrayWheelScope(
        MouseWheelMode mode,
        int notches,
        out string? primaryGdiDeviceName)
    {
        primaryGdiDeviceName = null;

        if (mode == MouseWheelMode.Disabled ||
            notches == 0 ||
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

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
    /// monitor can accept.
    /// </summary>
    public bool CanAdjustBrightnessFromTrayWheel =>
        PlanTrayWheelAdjustments(MouseWheelControlMode.Normalize(), 1).Count > 0;

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

        var brightnessValues = new int[adjustments.Count];
        for (var i = 0; i < adjustments.Count; i++)
        {
            var adjustment = adjustments[i];
            brightnessValues[i] = adjustment.Brightness;
            foreach (var monitor in Monitors)
            {
                if (MonitorIdComparer.Equal(monitor.Id, adjustment.Id))
                {
                    if (LinkedLevelsActive && IsLinkedTarget(monitor))
                    {
                        brightnessValues[i] = linkedBrightness;
                        hasLinkedTarget = true;
                    }
                    else
                    {
                        monitor.Brightness = adjustment.Brightness;
                    }

                    break;
                }
            }
        }

        if (hasLinkedTarget)
        {
            LinkedBrightness = linkedBrightness;
        }

        return new TrayWheelAdjustmentFeedback(mode, brightnessValues);
    }

    private IReadOnlyList<TrayWheelAdjustmentPlanner.Adjustment> PlanTrayWheelAdjustments(
        MouseWheelMode mode,
        int notches)
    {
        if (mode == MouseWheelMode.Disabled ||
            notches == 0 ||
            MouseWheelIncrement <= 0 ||
            !IsInitialized ||
            !IsInteractionEnabled)
        {
            return [];
        }

        string? primaryGdiDeviceName = null;
        if (mode == MouseWheelMode.PrimaryDisplay)
        {
            primaryGdiDeviceName = GetPrimaryGdiDeviceName();
        }

        var targets = new List<TrayWheelAdjustmentPlanner.Target>(Monitors.Count);
        foreach (var monitor in Monitors)
        {
            targets.Add(new TrayWheelAdjustmentPlanner.Target(
                monitor.Id,
                monitor.GdiDeviceName,
                monitor.SupportsBrightness,
                monitor.HasValidBrightnessReading,
                monitor.Brightness));
        }

        var delta = (long)notches * MouseWheelIncrement;
        return TrayWheelAdjustmentPlanner.Plan(
            mode,
            targets,
            primaryGdiDeviceName,
            delta);
    }

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

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
    /// Applies complete tray wheel notches to the configured brightness targets.
    /// </summary>
    /// <param name="notches">The signed number of complete wheel notches.</param>
    public void AdjustBrightnessFromTrayWheel(int notches)
    {
        var mode = MouseWheelControlMode.Normalize();
        if (mode == MouseWheelMode.Disabled ||
            notches == 0 ||
            MouseWheelIncrement <= 0 ||
            !IsInitialized ||
            !IsInteractionEnabled)
        {
            return;
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
        var adjustments = TrayWheelAdjustmentPlanner.Plan(
            mode,
            targets,
            primaryGdiDeviceName,
            delta);

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
        foreach (var adjustment in adjustments)
        {
            foreach (var monitor in Monitors)
            {
                if (MonitorIdComparer.Equal(monitor.Id, adjustment.Id))
                {
                    monitor.Brightness = adjustment.Brightness;
                    break;
                }
            }
        }
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

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Plans relative brightness changes for validated tray wheel input.
/// </summary>
public static class TrayWheelAdjustmentPlanner
{
    /// <summary>
    /// Describes a visible monitor's state needed for tray wheel planning.
    /// </summary>
    public readonly record struct Target(
        string Id,
        string GdiDeviceName,
        bool SupportsBrightness,
        bool HasBrightnessReading,
        int CurrentBrightness);

    /// <summary>
    /// Describes one monitor brightness update.
    /// </summary>
    public readonly record struct Adjustment(string Id, int Brightness);

    /// <summary>
    /// Determines whether one monitor would receive an update for the supplied mode. Split out of
    /// <see cref="Plan"/> so a caller that only needs to know whether any target exists at all can
    /// answer that without materializing the adjustment list.
    /// </summary>
    /// <param name="mode">The effective mouse-wheel mode.</param>
    /// <param name="target">The monitor state to test.</param>
    /// <param name="primaryGdiDeviceName">The primary logical display's GDI name.</param>
    /// <returns><see langword="true"/> when the monitor is a valid target.</returns>
    public static bool IsEligible(
        MouseWheelControlMode mode,
        Target target,
        string? primaryGdiDeviceName)
    {
        if (mode.Normalize() == MouseWheelControlMode.Disabled ||
            string.IsNullOrEmpty(target.Id) ||
            !target.SupportsBrightness ||
            !target.HasBrightnessReading)
        {
            return false;
        }

        if (mode != MouseWheelControlMode.PrimaryDisplay)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(primaryGdiDeviceName) &&
            string.Equals(
                target.GdiDeviceName,
                primaryGdiDeviceName,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Selects eligible targets and computes clamped brightness values.
    /// </summary>
    /// <param name="mode">The effective mouse-wheel mode.</param>
    /// <param name="targets">Visible monitor states.</param>
    /// <param name="primaryGdiDeviceName">The primary logical display's GDI name.</param>
    /// <param name="delta">The signed relative brightness delta.</param>
    /// <returns>Brightness updates in target enumeration order.</returns>
    public static IReadOnlyList<Adjustment> Plan(
        MouseWheelControlMode mode,
        IEnumerable<Target> targets,
        string? primaryGdiDeviceName,
        long delta)
    {
        ArgumentNullException.ThrowIfNull(targets);

        mode = mode.Normalize();
        if (mode == MouseWheelControlMode.Disabled || delta == 0)
        {
            return [];
        }

        if (mode == MouseWheelControlMode.PrimaryDisplay &&
            string.IsNullOrWhiteSpace(primaryGdiDeviceName))
        {
            return [];
        }

        var boundedDelta = Math.Clamp(delta, -100L, 100L);
        var adjustments = new List<Adjustment>();

        foreach (var target in targets)
        {
            if (!IsEligible(mode, target, primaryGdiDeviceName))
            {
                continue;
            }

            var brightness = (int)Math.Clamp(
                target.CurrentBrightness + boundedDelta,
                0,
                100);

            adjustments.Add(new Adjustment(target.Id, brightness));
        }

        return adjustments;
    }
}

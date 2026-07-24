// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Helpers;

namespace PowerDisplay.ViewModels;

public partial class MainViewModel
{
    private readonly SemaphoreSlim _hotkeyAdjustmentGate = new(1, 1);

    /// <summary>
    /// Applies a user-bound adjustment shortcut to every currently visible display that supports
    /// the requested setting. The shared mouse-wheel increment is also the shortcut step.
    /// </summary>
    internal async Task HandleAdjustmentHotkeyAsync(PowerDisplayHotkeyAction action)
    {
        if (!IsInteractionEnabled)
        {
            return;
        }

        await _hotkeyAdjustmentGate.WaitAsync();
        try
        {
            var step = Math.Clamp(MouseWheelIncrement, 1, 100);
            var delta = IsDecreaseAction(action) ? -step : step;
            var monitors = Monitors.ToList();
            List<Task> writes = action switch
            {
                PowerDisplayHotkeyAction.IncreaseBrightness or
                PowerDisplayHotkeyAction.DecreaseBrightness => BuildBrightnessWrites(monitors, delta),

                PowerDisplayHotkeyAction.IncreaseContrast or
                PowerDisplayHotkeyAction.DecreaseContrast => monitors
                    .Where(m => m.SupportsContrast)
                    .Select(m => m.SetContrastAsync(Adjust(m.Contrast, delta)))
                    .ToList(),

                PowerDisplayHotkeyAction.IncreaseVolume or
                PowerDisplayHotkeyAction.DecreaseVolume => monitors
                    .Where(m => m.SupportsVolume)
                    .Select(m => m.SetVolumeAsync(Adjust(m.Volume, delta)))
                    .ToList(),

                PowerDisplayHotkeyAction.IncreaseSdrContentBrightness or
                PowerDisplayHotkeyAction.DecreaseSdrContentBrightness => monitors
                    .Where(m => m.SupportsSdrContentBrightness)
                    .Select(m => m.SetSdrContentBrightnessAsync(Adjust(m.SdrContentBrightness, delta)))
                    .ToList(),

                _ => new List<Task>(),
            };

            if (writes.Count > 0)
            {
                await Task.WhenAll(writes);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Hotkey] Failed to apply {action}: {ex.Message}");
        }
        finally
        {
            _hotkeyAdjustmentGate.Release();
        }
    }

    private List<Task> BuildBrightnessWrites(IReadOnlyList<MonitorViewModel> monitors, int delta)
    {
        if (!LinkedLevelsActive)
        {
            return monitors
                .Where(m => m.SupportsPrimaryBrightness)
                .Select(m => m.SetPrimaryBrightnessAsync(Adjust(m.PrimaryBrightness, delta)))
                .ToList();
        }

        var linkedValue = Adjust(LinkedBrightness, delta);
        SetLinkedBrightnessSilently(linkedValue);

        return monitors
            .Where(m => m.SupportsPrimaryBrightness)
            .Select(m => m.SetPrimaryBrightnessAsync(
                IsLinkedTarget(m) ? linkedValue : Adjust(m.PrimaryBrightness, delta)))
            .ToList();
    }

    private static int Adjust(int value, int delta) => Math.Clamp(value + delta, 0, 100);

    private static bool IsDecreaseAction(PowerDisplayHotkeyAction action) =>
        action is PowerDisplayHotkeyAction.DecreaseBrightness
            or PowerDisplayHotkeyAction.DecreaseContrast
            or PowerDisplayHotkeyAction.DecreaseVolume
            or PowerDisplayHotkeyAction.DecreaseSdrContentBrightness;
}

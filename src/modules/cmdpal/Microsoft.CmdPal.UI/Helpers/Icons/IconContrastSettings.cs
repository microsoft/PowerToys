// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.System;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Updates a shared contrast snapshot on Windows settings notifications, without per-icon system reads.</summary>
internal static class IconContrastSettings
{
    internal static event Action? Changed;

    private static readonly UISettings UiSettings = new();
    private static readonly Lock Sync = new();

    private static ContrastState _state = new(default);
    private static DispatcherQueue? _dispatcherQueue;
    private static ThemeSettings? _themeSettings;

    internal static IconContrast Current => Volatile.Read(ref _state).Contrast;

    /// <summary>Starts desktop contrast notifications from the palette window, which lives until app shutdown.</summary>
    internal static void Initialize(WindowId windowId)
    {
        if (_themeSettings is not null)
        {
            return;
        }

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _themeSettings = ThemeSettings.CreateForWindowId(windowId);
        _themeSettings.Changed += (_, _) => RefreshOnOwnerThread();
        UiSettings.ColorValuesChanged += (_, _) => RefreshOnOwnerThread();
        Refresh();
    }

    // ThemeSettings is bound to the UI thread; UISettings raises ColorValuesChanged on a worker thread.
    private static void RefreshOnOwnerThread()
    {
        var queue = _dispatcherQueue!;
        if (queue.HasThreadAccess)
        {
            Refresh();
        }
        else
        {
            queue.TryEnqueue(Refresh);
        }
    }

    private static void Refresh()
    {
        lock (Sync)
        {
            var contrast = ReadContrast();
            if (_state.Contrast == contrast)
            {
                return;
            }

            Volatile.Write(ref _state, new(contrast));
        }

        Changed?.Invoke();
    }

    private static IconContrast ReadContrast()
    {
        if (_themeSettings?.HighContrast != true)
        {
            return default;
        }

        var foreground = UiSettings.GetColorValue(UIColorType.Foreground);
        var background = UiSettings.GetColorValue(UIColorType.Background);
        var mode = background.R + background.G + background.B >= 384
            ? IconContrastMode.White : IconContrastMode.Black;
        try
        {
            // Read the OS qualifier rather than guessing from a contrast theme's filename.
            var qualifier = new ResourceManager().CreateResourceContext().QualifierValues["Contrast"];
            mode = qualifier.ToLowerInvariant() switch
            {
                "white" => IconContrastMode.White,
                "black" => IconContrastMode.Black,
                "high" => IconContrastMode.High,
                _ => mode,
            };
        }
        catch
        {
            // Background brightness remains a usable fallback when MRT is unavailable.
        }

        return new(mode, ToArgb(foreground), ToArgb(background));
    }

    private static uint ToArgb(global::Windows.UI.Color color)
    {
        return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }

    private sealed record ContrastState(IconContrast Contrast);
}

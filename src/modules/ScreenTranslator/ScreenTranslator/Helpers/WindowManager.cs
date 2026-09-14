// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.Helpers;

public static class WindowManager
{
    private static readonly List<SelectionOverlay> SelectionWindows = new();
    private static readonly List<ResultOverlay> ResultWindows = new();
    private static readonly object Lock = new();
    private static ProcessingOverlay? _processingWindow;
    private static PhysicalRect? _foregroundWindowBounds;
    private static IntPtr _foregroundWindowHandle;

    public static void LaunchScreenTranslatorOnEveryScreen()
    {
        CloseAllOverlays();

        Logger.LogInfo("Launching ScreenTranslator selection overlay on every screen");

        ScreenTranslatorSettings? settings = null;
        try
        {
            settings = SettingsUtils.Default.GetSettingsOrDefault<ScreenTranslatorSettings>("ScreenTranslator");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load ScreenTranslator settings: {ex.Message}");
        }

        var provider = TranslationProviderFactory.Create(settings);
        var sourceLang = settings?.Properties?.SourceLanguage ?? "auto";
        var targetLang = settings?.Properties?.TargetLanguage ?? "en-US";
        (_foregroundWindowHandle, _foregroundWindowBounds) = CaptureForegroundWindow();

        var screens = MonitorHelper.GetAllScreens();

        lock (Lock)
        {
            foreach (var screen in screens)
            {
                SelectionOverlay overlay = new(screen, provider, sourceLang, targetLang, _foregroundWindowBounds);
                overlay.Closed += (s, e) =>
                {
                    lock (Lock)
                    {
                        SelectionWindows.Remove(overlay);
                    }
                };

                SelectionWindows.Add(overlay);
                overlay.Show();
            }
        }
    }

    private static (IntPtr Handle, PhysicalRect? Bounds) CaptureForegroundWindow()
    {
        IntPtr foregroundWindow = OSInterop.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero ||
            !OSInterop.IsWindowVisible(foregroundWindow) ||
            !OSInterop.GetWindowRect(foregroundWindow, out OSInterop.RECT windowRect) ||
            windowRect.Width <= 0 ||
            windowRect.Height <= 0)
        {
            return (IntPtr.Zero, null);
        }

        return (foregroundWindow, new PhysicalRect(windowRect.Left, windowRect.Top, windowRect.Width, windowRect.Height));
    }

    public static void CloseAllSelectionOverlays()
    {
        SelectionOverlay[] windowsToClose;
        lock (Lock)
        {
            windowsToClose = SelectionWindows.ToArray();
            SelectionWindows.Clear();
        }

        foreach (var window in windowsToClose)
        {
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception closing SelectionOverlay: {ex.Message}");
            }
        }
    }

    public static void CloseAllResultOverlays()
    {
        ResultOverlay[] windowsToClose;
        lock (Lock)
        {
            windowsToClose = ResultWindows.ToArray();
            ResultWindows.Clear();
        }

        foreach (var window in windowsToClose)
        {
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception closing ResultOverlay: {ex.Message}");
            }
        }
    }

    public static void CloseProcessingOverlay(ProcessingOverlay? overlay, bool cancelOperation = false)
    {
        if (overlay == null)
        {
            return;
        }

        lock (Lock)
        {
            if (!ReferenceEquals(_processingWindow, overlay))
            {
                return;
            }

            _processingWindow = null;
        }

        try
        {
            if (cancelOperation)
            {
                overlay.RequestCancellation();
            }

            overlay.Close();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Exception closing ProcessingOverlay: {ex.Message}");
        }
    }

    public static ProcessingOverlay ShowProcessingOverlay(PhysicalRect capturedRegion, Action cancelOperation)
    {
        ProcessingOverlay? previous;
        lock (Lock)
        {
            previous = _processingWindow;
        }

        CloseProcessingOverlay(previous, cancelOperation: true);

        ScreenInfo targetScreen = FindTargetScreen(capturedRegion);
        ProcessingOverlay overlay = new(targetScreen, capturedRegion, cancelOperation);
        overlay.Closed += (s, e) =>
        {
            lock (Lock)
            {
                if (ReferenceEquals(_processingWindow, overlay))
                {
                    _processingWindow = null;
                }
            }
        };

        lock (Lock)
        {
            _processingWindow = overlay;
        }

        overlay.Show();
        return overlay;
    }

    public static void CloseAllOverlays()
    {
        CloseAllSelectionOverlays();
        CloseAllResultOverlays();

        ProcessingOverlay? processingWindow;
        lock (Lock)
        {
            processingWindow = _processingWindow;
        }

        CloseProcessingOverlay(processingWindow, cancelOperation: true);
    }

    public static void ShowResultOverlay(
        PhysicalRect capturedRegion,
        IReadOnlyList<TranslatedLine> lines,
        string sourceLanguage = "auto",
        string targetLanguage = "en-US",
        Func<string, string, Task>? retranslateAll = null,
        Func<TranslationLine, string, string, Task<TranslationResult>>? retranslateLine = null)
    {
        CloseAllResultOverlays();

        if (lines == null || lines.Count == 0)
        {
            Logger.LogInfo("No translated lines to display in ResultOverlay.");
            return;
        }

        ScreenInfo targetScreen = FindTargetScreen(capturedRegion);

        Logger.LogInfo($"Displaying ResultOverlay on screen {targetScreen.Bounds.X},{targetScreen.Bounds.Y} with {lines.Count} lines.");
        ResultOverlay overlay;
        try
        {
            overlay = new ResultOverlay(
                targetScreen,
                capturedRegion,
                lines,
                _foregroundWindowHandle,
                sourceLanguage,
                targetLanguage,
                retranslateAll,
                retranslateLine);
            Logger.LogInfo("ResultOverlay constructed successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to construct ResultOverlay: {ex}");
            return;
        }

        overlay.Closed += (s, e) =>
        {
            lock (Lock)
            {
                ResultWindows.Remove(overlay);
            }
        };

        lock (Lock)
        {
            ResultWindows.Add(overlay);
        }

        try
        {
            overlay.Show();
            Logger.LogInfo("ResultOverlay shown successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to show ResultOverlay: {ex}");
            lock (Lock)
            {
                ResultWindows.Remove(overlay);
            }

            try
            {
                overlay.Close();
            }
            catch (Exception closeException)
            {
                Logger.LogWarning($"Failed to close ResultOverlay after show failure: {closeException.Message}");
            }
        }
    }

    private static ScreenInfo FindTargetScreen(PhysicalRect capturedRegion)
    {
        var screens = MonitorHelper.GetAllScreens();
        List<PhysicalRect> screenRects = new();
        foreach (var screen in screens)
        {
            screenRects.Add(screen.Bounds);
        }

        PhysicalRect? targetScreenRect = OverlayLayoutHelper.FindContainingScreen(capturedRegion, screenRects);
        ScreenInfo targetScreen = screens[0];

        if (targetScreenRect.HasValue)
        {
            foreach (var screen in screens)
            {
                if (screen.Bounds.X == targetScreenRect.Value.X && screen.Bounds.Y == targetScreenRect.Value.Y)
                {
                    targetScreen = screen;
                    break;
                }
            }
        }

        return targetScreen;
    }
}

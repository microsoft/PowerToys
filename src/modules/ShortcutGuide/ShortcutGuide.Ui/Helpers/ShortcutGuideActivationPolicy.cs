// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace ShortcutGuide.Helpers;

public enum ShortcutGuideActivationSource
{
    None,
    RegularHotkey,
    WindowsKeyHold,
}

public enum ShortcutGuideOverlaySurface
{
    Hidden,
    TaskbarIndicators,
    FullGuide,
}

public enum ShortcutGuideActivationAction
{
    None,
    ShowTaskbarIndicators,
    ShowFullGuide,
    Close,
}

public static class ShortcutGuideActivationPolicy
{
    public static ShortcutGuideActivationAction GetActivationAction(
        ShortcutGuideActivationSource activationSource,
        bool isOverlayVisible,
        bool isCurrentWindowExcluded,
        ShortcutGuideActivationSource activeSource,
        ShortcutGuideOverlaySurface activeSurface,
        ShortcutGuideWindowsKeyAction windowsKeyAction)
    {
        if (!isOverlayVisible && isCurrentWindowExcluded)
        {
            return ShortcutGuideActivationAction.None;
        }

        if (!isOverlayVisible)
        {
            activeSource = ShortcutGuideActivationSource.None;
            activeSurface = ShortcutGuideOverlaySurface.Hidden;
        }

        if (activationSource == ShortcutGuideActivationSource.RegularHotkey)
        {
            if (activeSource == ShortcutGuideActivationSource.WindowsKeyHold ||
                activeSurface == ShortcutGuideOverlaySurface.TaskbarIndicators ||
                activeSurface == ShortcutGuideOverlaySurface.Hidden)
            {
                return ShortcutGuideActivationAction.ShowFullGuide;
            }

            return ShortcutGuideActivationAction.Close;
        }

        if (activationSource != ShortcutGuideActivationSource.WindowsKeyHold ||
            windowsKeyAction == ShortcutGuideWindowsKeyAction.Off ||
            isOverlayVisible ||
            activeSurface != ShortcutGuideOverlaySurface.Hidden)
        {
            return ShortcutGuideActivationAction.None;
        }

        return windowsKeyAction == ShortcutGuideWindowsKeyAction.OpenShortcutGuide
            ? ShortcutGuideActivationAction.ShowFullGuide
            : ShortcutGuideActivationAction.ShowTaskbarIndicators;
    }

    public static bool ShouldCloseOnWindowsKeyRelease(
        ShortcutGuideActivationSource activeSource,
        ShortcutGuideOverlaySurface activeSurface,
        bool closeFullGuideOnRelease)
    {
        if (activeSource != ShortcutGuideActivationSource.WindowsKeyHold)
        {
            return false;
        }

        return activeSurface == ShortcutGuideOverlaySurface.TaskbarIndicators ||
               (activeSurface == ShortcutGuideOverlaySurface.FullGuide && closeFullGuideOnRelease);
    }
}

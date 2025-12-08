// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;

namespace FancyZonesCLI;

/// <summary>
/// Native Windows API methods for FancyZones CLI.
/// </summary>
internal static class NativeMethods
{
    // Registered Windows messages for notifying FancyZones
    private static uint wmPrivAppliedLayoutsFileUpdate;
    private static uint wmPrivLayoutHotkeysFileUpdate;

    /// <summary>
    /// Gets the Windows message ID for applied layouts file update notification.
    /// </summary>
    public static uint WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE => wmPrivAppliedLayoutsFileUpdate;

    /// <summary>
    /// Gets the Windows message ID for layout hotkeys file update notification.
    /// </summary>
    public static uint WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE => wmPrivLayoutHotkeysFileUpdate;

    /// <summary>
    /// Initializes the Windows messages used for FancyZones notifications.
    /// </summary>
    public static void InitializeWindowMessages()
    {
        wmPrivAppliedLayoutsFileUpdate = PInvoke.RegisterWindowMessage("{2ef2c8a7-e0d5-4f31-9ede-52aade2d284d}");
        wmPrivLayoutHotkeysFileUpdate = PInvoke.RegisterWindowMessage("{07229b7e-4f22-4357-b136-33c289be2295}");
    }

    /// <summary>
    /// Broadcasts a notification message to FancyZones.
    /// </summary>
    /// <param name="message">The Windows message ID to broadcast.</param>
    public static void NotifyFancyZones(uint message)
    {
        PInvoke.PostMessage(HWND.HWND_BROADCAST, message, 0, 0);
    }

    /// <summary>
    /// Brings the specified window to the foreground.
    /// </summary>
    /// <param name="hWnd">A handle to the window.</param>
    /// <returns>True if the window was brought to the foreground.</returns>
    public static bool SetForegroundWindow(nint hWnd)
    {
        return PInvoke.SetForegroundWindow(new HWND(hWnd));
    }
}

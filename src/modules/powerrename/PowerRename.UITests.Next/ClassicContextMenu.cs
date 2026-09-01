// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// Reads the item captions of the real <c>HMENU</c> behind a classic (<c>#32768</c>) context-menu
/// window, by asking the window for its menu handle (<c>MN_GETHMENU</c>).
/// </summary>
/// <remarks>
/// This is the authoritative view of what the shell put in the menu: exact captions, no dependency on
/// the transient popup's UIA tree, and a miss can report the full inventory instead of just "not
/// found". Measured on Windows 10 and 11, <c>GetMenuItemInfo(MIIM_BITMAP)</c> returns a null
/// <c>hbmpItem</c> for every Explorer menu item, so icon presence is measured from pixels instead.
/// </remarks>
internal static class ClassicContextMenu
{
    /// <summary>Window class of a classic Win32 popup menu.</summary>
    public const string WindowClassName = "#32768";

    private const uint MNGETHMENU = 0x01E1;
    private const uint MFBYPOSITION = 0x00000400;
    private const uint SMTOABORTIFHUNG = 0x0002;

    /// <summary>
    /// Captions of every item of the popup menu owned by <paramref name="menuWindow"/>, or null when
    /// the window no longer owns a menu (a transient popup can vanish mid-read).
    /// </summary>
    public static IReadOnlyList<string>? TryReadItemCaptions(IntPtr menuWindow)
    {
        if (menuWindow == IntPtr.Zero)
        {
            return null;
        }

        if (SendMessageTimeoutW(menuWindow, MNGETHMENU, IntPtr.Zero, IntPtr.Zero, SMTOABORTIFHUNG, 2_000, out var menu) == IntPtr.Zero ||
            menu == IntPtr.Zero)
        {
            return null;
        }

        var count = GetMenuItemCount(menu);
        if (count <= 0)
        {
            return null;
        }

        var captions = new List<string>(count);
        var buffer = new StringBuilder(512);
        for (var index = 0; index < count; index++)
        {
            buffer.Clear();
            var length = GetMenuStringW(menu, (uint)index, buffer, buffer.Capacity, MFBYPOSITION);
            captions.Add(NormalizeCaption(length > 0 ? buffer.ToString() : string.Empty));
        }

        return captions;
    }

    private static string NormalizeCaption(string caption)
    {
        var accelerator = caption.IndexOf('\t');
        if (accelerator >= 0)
        {
            caption = caption[..accelerator];
        }

        return caption.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeoutMS,
        out IntPtr result);

    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(IntPtr menu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuStringW(IntPtr menu, uint item, StringBuilder text, int maxCount, uint flags);
}

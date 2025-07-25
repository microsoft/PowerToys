// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public static partial class NativeMethods
{
    private const int WS_POPUP = 1 << 31; // 0x80000000
    internal const int GWL_STYLE = -16;
    internal const int WS_CAPTION = 0x00C00000;
    internal const int SPI_GETDESKWALLPAPER = 0x0073;
    internal const int SW_SHOWNORMAL = 1;
    internal const int SW_SHOWMAXIMIZED = 3;
    internal const int SW_HIDE = 0;

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

    [LibraryImport("user32.dll")]
    internal static partial uint SendInput(uint nInputs, NativeKeyboardHelper.INPUT[] pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    internal static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // [DllImport("shell32.dll")]
    // internal static extern IntPtr SHBrowseForFolderW(ref ShellGetFolder.BrowseInformation browseInfo);
    [LibraryImport("shell32.dll")]
    internal static partial int SHGetPathFromIDListW(IntPtr pidl, IntPtr pszPath);

    // [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode)]
    // internal static extern bool GetOpenFileName([In, Out] OpenFileName openFileName);
#pragma warning disable CA1401 // P/Invokes should not be visible
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    public static partial int GetDpiForWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int dwProcessId);

    [System.Runtime.InteropServices.DllImport("User32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr LoadLibrary(string dllToLoad);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool FreeLibrary(IntPtr hModule);

#pragma warning restore CA1401 // P/Invokes should not be visible

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]

    internal static extern bool SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);

    public static void SetPopupStyle(IntPtr hwnd)
    {
        _ = SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) | WS_POPUP);
    }
}

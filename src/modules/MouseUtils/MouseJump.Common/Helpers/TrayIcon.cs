// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

using MouseJump.Common.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.Common.Helpers;

public sealed class TrayIcon
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const uint WM_TRAYICON_SHOWCONTEXTMENU = PInvoke.WM_USER + 100;
    private const uint WM_TRAYICON_EXITCOMMAND = PInvoke.WM_USER + 101;

    private const uint WM_UAHINITMENU = 0x0093;
    private const uint WM_UAHMEASUREMENUITEM = 0x0094;
#pragma warning restore SA1310 // Field names should not contain underscore

    // initialise with an empty delegate so we don't get complaints about
    // it being null when the constructor exits.
    public event EventHandler<EventArgs> ExitCommandClicked = (sender, e) => { };

    public TrayIcon()
    {
        this.Initialize();
    }

    private Win32Window Window
    {
        get;
        set;
    }

    private Icon Icon
    {
        get;
        set;
    }

    private HMENU ContextMenu
    {
        get;
        set;
    }

    private void OnExitCommandClicked(EventArgs e)
    {
        this.ExitCommandClicked.Invoke(this, e);
    }

    private HWND GetHwndOrThrow()
    {
        return (HWND)(this.Window?.Hwnd ?? throw new InvalidOperationException());
    }

    [MemberNotNull(nameof(Window))]
    [MemberNotNull(nameof(Icon))]
    private void Initialize()
    {
        this.InitializeTrayWindow();
        this.InitializeTrayIcon();
        this.InitializeTrayMenu();
    }

    [MemberNotNull(nameof(Window))]
    private void InitializeTrayWindow()
    {
        this.Window = Win32Helper.CreateMessageOnlyWindow(
            className: "FancyMouseTrayIconClass",
            windowName: "FancyMouseTrayIconWindow",
            windowProcDelegate: this.WindowProc);
    }

    [MemberNotNull(nameof(Icon))]
    private void InitializeTrayIcon()
    {
        var icon = TrayIcon.GetTrayIconResource();
        this.Icon = icon;

        var notifyIconData = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = this.GetHwndOrThrow(),
            uID = 1,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
            uCallbackMessage = TrayIcon.WM_TRAYICON_SHOWCONTEXTMENU,
            hIcon = (HICON)icon.Handle,
            szTip = "FancyMouse",
            dwState = 0,
            dwStateMask = 0,
            szInfo = string.Empty,
            Anonymous = default,
            szInfoTitle = string.Empty,
            dwInfoFlags = 0,
            guidItem = Guid.Empty,
            hBalloonIcon = HICON.Null,
        };
        var result = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, notifyIconData);
        ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.Shell_NotifyIcon));
    }

    private void InitializeTrayMenu()
    {
        var hMenu = PInvoke.CreatePopupMenu();
        ResultHandler.ThrowIfZero(hMenu, getLastError: true, nameof(PInvoke.CreatePopupMenu));

        using var safeMenuHandle = new Win32SafeHandle(hMenu);
        var result = PInvoke.AppendMenu(safeMenuHandle, MENU_ITEM_FLAGS.MF_STRING, TrayIcon.WM_TRAYICON_EXITCOMMAND, "Exit");
        ResultHandler.ThrowIfZero(hMenu, getLastError: true, nameof(PInvoke.AppendMenu));

        this.ContextMenu = hMenu;
    }

    private static Icon GetTrayIconResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "FancyMouse.Common.images.icon.ico";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException();
        var icon = new Icon(stream);
        return icon;
    }

    private nint WindowProc(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            // tray icon callback message
            case TrayIcon.WM_TRAYICON_SHOWCONTEXTMENU:
                if ((uint)lParam is PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN)
                {
                    // show the context menu associated with the tray icon
                    TrayIcon.ShowTrayIconContextMenu((HWND)hWnd, this.ContextMenu);
                }

                break;

            /* received during tray icon create */
            case PInvoke.WM_GETMINMAXINFO:
            case PInvoke.WM_NCCREATE:
            case PInvoke.WM_NCCALCSIZE:
            case PInvoke.WM_CREATE:
                break;

            /* context menu display */
            case PInvoke.WM_WINDOWPOSCHANGING:
            case PInvoke.WM_WINDOWPOSCHANGED:
            case PInvoke.WM_NCACTIVATE:
            case PInvoke.WM_ACTIVATE:
            case PInvoke.WM_IME_SETCONTEXT:
            case PInvoke.WM_IME_NOTIFY:
            case PInvoke.WM_SETFOCUS:
                break;

            /* context menu message loop */
            case PInvoke.WM_ENTERMENULOOP:
            case PInvoke.WM_SETCURSOR:
            case PInvoke.WM_INITMENU:
            case PInvoke.WM_INITMENUPOPUP:
            case TrayIcon.WM_UAHINITMENU:
            case TrayIcon.WM_UAHMEASUREMENUITEM:
            case PInvoke.WM_ENTERIDLE:
            case PInvoke.WM_MENUSELECT:
            case PInvoke.WM_UNINITMENUPOPUP:
            case PInvoke.WM_EXITMENULOOP:
            case PInvoke.WM_KILLFOCUS:
                break;

            /* menu item clicked */
            case PInvoke.WM_COMMAND:
                const long lowWordMask = 0x0000FFFF;
                var commandIndex = wParam & lowWordMask;
                switch (commandIndex)
                {
                    case TrayIcon.WM_TRAYICON_EXITCOMMAND:
                        this.OnExitCommandClicked(EventArgs.Empty);
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                break;

            default:
                break;
        }

        return PInvoke.DefWindowProc((HWND)hWnd, msg, wParam, lParam);
    }

    private static void ShowTrayIconContextMenu(HWND hWnd, HMENU hMenu)
    {
        if (hMenu.IsNull)
        {
            throw new InvalidOperationException();
        }

        var result = PInvoke.SetForegroundWindow(hWnd);
        ResultHandler.ThrowIfZero(result, getLastError: false, nameof(PInvoke.SetForegroundWindow));

        // get the current cursor position
        result = PInvoke.GetCursorPos(out var cursorPos);
        ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.GetCursorPos));

        // convert it into client coordinates
        result = PInvoke.ScreenToClient(hWnd, ref cursorPos);
        ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.ScreenToClient));

        // set menu information
        var menuInfo = new MENUINFO
        {
            cbSize = (uint)Marshal.SizeOf<MENUINFO>(),
            fMask = MENUINFO_MASK.MIM_STYLE,
            dwStyle = 0,
            cyMax = 0,
            hbrBack = HBRUSH.Null,
            dwContextHelpID = 0,
            dwMenuData = nuint.Zero,
        };
        using var safeMenuHandle = new Win32SafeHandle(hMenu);
        result = PInvoke.SetMenuInfo(safeMenuHandle, menuInfo);
        ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.SetMenuInfo));

        // display the context menu at the cursor position
        result = PInvoke.TrackPopupMenuEx(
              safeMenuHandle,
              (uint)(TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN | TRACK_POPUP_MENU_FLAGS.TPM_LEFTBUTTON),
              cursorPos.X,
              cursorPos.Y,
              hWnd,
              null);
        ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.TrackPopupMenuEx));
    }
}

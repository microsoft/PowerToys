// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Menus;

public partial class MenusCommandsProvider : CommandProvider
{
    public MenusCommandsProvider()
    {
        DisplayName = $"Menus from the open windows";
    }

    private readonly ICommandItem[] _commands = [
        new CommandItem(new AllWindowsPage()) { Subtitle = "Search menus in open windows" },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class MenuItemCommand : InvokableCommand
{
    private readonly MenuData _menuData;
    private readonly HWND _hwnd;

    public MenuItemCommand(MenuData data, HWND hwnd)
    {
        _menuData = data;
        _hwnd = hwnd;
    }

    public override ICommandResult Invoke()
    {
        PInvoke.SetForegroundWindow(_hwnd);
        PInvoke.SetActiveWindow(_hwnd);
        PInvoke.PostMessage(_hwnd, 273/*WM_COMMAND*/, _menuData.WID, 0);
        return CommandResult.KeepOpen();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed class MenuData
{
    public string ItemText { get; set; }

    public string PathText { get; set; }

    public uint WID { get; set; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed class WindowData
{
    private readonly HWND handle;

    private readonly string title = string.Empty;

    public string Title => title;

    public HWND Handle => handle;

    internal WindowData(HWND hWnd)
    {
        handle = hWnd;
        var textLen = PInvoke.GetWindowTextLength(handle);
        if (textLen == 0)
        {
            return;
        }

        var bufferSize = textLen + 1;
        unsafe
        {
            fixed (char* windowNameChars = new char[bufferSize])
            {
                if (PInvoke.GetWindowText(handle, windowNameChars, bufferSize) == 0)
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    if (errorCode != 0)
                    {
                        throw new Win32Exception(errorCode);
                    }
                }

                title = new string(windowNameChars);
            }
        }
    }

    public List<MenuData> GetMenuItems()
    {
        var hMenu = PInvoke.GetMenu_SafeHandle(handle);
        return GetMenuItems(hMenu, string.Empty);
    }

    public List<MenuData> GetMenuItems(DestroyMenuSafeHandle hMenu, string menuPath)
    {
        // var s = new SafeMenu();
        // s.SetHandle(hMenu);
        List<MenuData> results = new();
        var menuItemCount = PInvoke.GetMenuItemCount(hMenu);
        for (var i = 0; i < menuItemCount; i++)
        {
            var mii = default(MENUITEMINFOW);
            mii.cbSize = (uint)Marshal.SizeOf<MENUITEMINFOW>();
            mii.fMask = MENU_ITEM_MASK.MIIM_STRING | MENU_ITEM_MASK.MIIM_ID | MENU_ITEM_MASK.MIIM_SUBMENU;
            mii.cch = 256;

            unsafe
            {
                fixed (char* menuTextBuffer = new char[mii.cch])
                {
                    mii.dwTypeData = new PWSTR(menuTextBuffer); // Allocate memory for string

                    if (PInvoke.GetMenuItemInfo(hMenu, (uint)i, true, ref mii))
                    {
                        var itemText = mii.dwTypeData.ToString();

                        // Sanitize it. If it's got a tab, grab the text before that:
                        var withoutShortcut = itemText.Split("\t").First();

                        // Now remove a `&`
                        var sanitized = withoutShortcut.Replace("&", string.Empty);

                        var itemPath = $"{menuPath}{sanitized}";

                        // Leaf item
                        if (mii.hSubMenu == IntPtr.Zero)
                        {
                            // Console.WriteLine($"- Leaf Item: {itemText}");
                            // TriggerMenuItem(hWnd, mii.wID);
                            var data = new MenuData() { ItemText = sanitized, PathText = itemPath, WID = mii.wID };
                            results.Add(data);
                        }
                        else
                        {
                            // Recursively list submenu items
                            var subMenuTest = PInvoke.GetSubMenu(hMenu, i);
                            var otherTest = mii.hSubMenu;
                            _ = otherTest == subMenuTest.DangerousGetHandle();
                            var newPath = $"{sanitized} > ";
                            var subItems = GetMenuItems(subMenuTest, newPath);
                            results.AddRange(subItems);
                        }
                    }
                }
            }
        }

        return results;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class WindowMenusPage : ListPage
{
    private readonly WindowData _window;

    public WindowMenusPage(WindowData window)
    {
        _window = window;
        Icon = new(string.Empty);
        Name = window.Title;
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        return _window.GetMenuItems().Select(menuData => new ListItem(new MenuItemCommand(menuData, _window.Handle)) { Title = menuData.ItemText, Subtitle = menuData.PathText }).ToArray();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class AllWindowsPage : ListPage
{
    private readonly List<WindowData> windows = new();

    public AllWindowsPage()
    {
        Icon = new("\uf0b5"); // ChecklistMirrored
        Name = "Open Windows";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        PInvoke.EnumWindows(EnumWindowsCallback, IntPtr.Zero);

        return windows
            .Where(w => !string.IsNullOrEmpty(w.Title))
            .Select(w => new ListItem(new WindowMenusPage(w))
            {
                Title = w.Title,
            })
            .ToArray();
    }

    private BOOL EnumWindowsCallback(HWND hWnd, LPARAM lParam)
    {
        // Only consider top-level visible windows with menus
        if (/*PInvoke.IsWindowVisible(hWnd) &&*/ PInvoke.GetMenu(hWnd) != IntPtr.Zero)
        {
            try
            {
                windows.Add(new(hWnd));
            }
            catch (Exception)
            {
            }

            return true;
        }

        return true; // Continue enumeration
    }
}

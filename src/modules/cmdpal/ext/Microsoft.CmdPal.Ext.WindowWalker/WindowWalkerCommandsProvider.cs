// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Pages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Collections;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Ext.WindowWalker;

public partial class WindowWalkerCommandsProvider : CommandProvider, IExtendedAttributesProvider
{
    private readonly CommandItem _windowWalkerPageItem;
    private readonly CommandItem _bandItem;
    private readonly SettingsManager _settings = SettingsManager.Instance;
    internal static readonly VirtualDesktopHelper VirtualDesktopHelperInstance = new();

    public WindowWalkerCommandsProvider()
    {
        _settings = new();
        Id = "WindowWalker";
        DisplayName = Resources.windowwalker_name;
        Icon = Icons.WindowWalkerIcon;
        Settings = _settings.Settings;

        _windowWalkerPageItem = new CommandItem(new WindowWalkerListPage(_settings))
        {
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_name,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
        _bandItem = new WindowsDockBand();
    }

    public override ICommandItem[] TopLevelCommands() => [_windowWalkerPageItem];

    public IDictionary<string, object> GetProperties()
    {
        return new PropertySet()
        {
            { "DockBands", new ICommandItem[] { _bandItem } },
        };
    }
}

#pragma warning disable SA1402 // File may only contain a single type

internal sealed partial class WindowsDockBand : CommandItem
{
    private WINEVENTPROC _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private WindowWalkerListPage _page;

    public WindowsDockBand()
    {
        Title = Resources.window_walker_top_level_command_title;
        Subtitle = Resources.windowwalker_name;

        var testSettings = new SettingsManager();
        testSettings.HideExplorerSettingInfo = true;
        testSettings.InMruOrder = false;
        testSettings.ResultsFromVisibleDesktopOnly = true;
        testSettings.UseWindowIcon = true;
        testSettings.ShowSubtitles = false;
        testSettings.ShowTitlesOnDock = SettingsManager.Instance.ShowTitlesOnDock;
        var testPage = new WindowWalkerListPage(testSettings);
        testPage.Id = "com.microsoft.cmdpal.windowwalker.dockband";
        _page = testPage;
        Command = testPage;

        // install window event hook
        _hookProc = (WINEVENTPROC)WinEventCallback;
        _hookHandle = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_CREATE,
            PInvoke.EVENT_OBJECT_NAMECHANGE, // include name/title changes
            HMODULE.Null,
            _hookProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
    }

    private void WinEventCallback(
        HWINEVENTHOOK hWinEventHook,
        uint eventType,
        HWND hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (idObject != (int)OBJECT_IDENTIFIER.OBJID_WINDOW ||
            hwnd == IntPtr.Zero)
        {
            return;
        }

        switch (eventType)
        {
            case PInvoke.EVENT_OBJECT_CREATE:
            case PInvoke.EVENT_OBJECT_SHOW:
            // TryAddWindow(hwnd);
            // break;
            case PInvoke.EVENT_OBJECT_DESTROY:
            case PInvoke.EVENT_OBJECT_HIDE:
            // TryRemoveWindow(hwnd);
            // break;
            case PInvoke.EVENT_OBJECT_NAMECHANGE:
                // TryUpdateWindow(hwnd);
                _page.RaiseItemsChanged();
                break;
        }
    }
}
#pragma warning restore SA1402 // File may only contain a single type

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WindowsTerminal.Commands;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Pages;

internal sealed partial class ProfilesListPage : ListPage
{
    private readonly TerminalQuery _terminalQuery = new();
    private readonly SettingsManager _terminalSettings;
    private readonly Dictionary<string, BitmapImage> _logoCache = [];

    private bool showHiddenProfiles;
    private bool openNewTab;
    private bool openQuake;

    public ProfilesListPage(SettingsManager terminalSettings)
    {
        Icon = WindowsTerminalCommandsProvider.TerminalIcon;
        Name = Resources.profiles_list_page_name;
        _terminalSettings = terminalSettings;
    }

#pragma warning disable SA1108
    public List<ListItem> Query()
    {
        showHiddenProfiles = _terminalSettings.ShowHiddenProfiles;
        openNewTab = _terminalSettings.OpenNewTab;
        openQuake = _terminalSettings.OpenQuake;

        var profiles = _terminalQuery.GetProfiles();

        var result = new List<ListItem>();

        foreach (var profile in profiles)
        {
            if (profile.Hidden && !showHiddenProfiles)
            {
                continue;
            }

            result.Add(new ListItem(new LaunchProfileCommand(profile.Terminal.AppUserModelId, profile.Name, profile.Terminal.LogoPath, openNewTab, openQuake))
            {
                Title = profile.Name,
                Subtitle = profile.Terminal.DisplayName,
                MoreCommands = [
                    new CommandContextItem(new LaunchProfileAsAdminCommand(profile.Terminal.AppUserModelId, profile.Name, openNewTab, openQuake)),
                ],

                // Icon = () => GetLogo(profile.Terminal),
                // Action = _ =>
                // {
                //    Launch(profile.Terminal.AppUserModelId, profile.Name);
                //    return true;
                // },
                // ContextData = profile,
#pragma warning restore SA1108
            });
        }

        return result;
    }

    public override IListItem[] GetItems() => Query().ToArray();

    private BitmapImage GetLogo(TerminalPackage terminal)
    {
        var aumid = terminal.AppUserModelId;

        if (!_logoCache.TryGetValue(aumid, out var value))
        {
            value = terminal.GetLogo();
            _logoCache.Add(aumid, value);
        }

        return value;
    }
}

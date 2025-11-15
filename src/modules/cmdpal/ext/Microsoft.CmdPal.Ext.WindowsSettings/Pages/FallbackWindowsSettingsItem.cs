// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowsSettings.Commands;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Pages;

internal sealed partial class FallbackWindowsSettingsItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.windows.settings.fallback";

    private readonly Classes.WindowsSettings _windowsSettings;

    private readonly string _title = Resources.settings_fallback_title;
    private readonly string _subtitle = Resources.settings_fallback_subtitle;

    public FallbackWindowsSettingsItem(Classes.WindowsSettings windowsSettings)
        : base(new NoOpCommand(), Resources.settings_title, _id)
    {
        Icon = Icons.WindowsSettingsIcon;
        _windowsSettings = windowsSettings;
    }

    public override void UpdateQuery(string query)
    {
        Command = new NoOpCommand();
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = null;
        MoreCommands = null;

        if (string.IsNullOrWhiteSpace(query) ||
            _windowsSettings?.Settings is null)
        {
            return;
        }

        var filteredList = _windowsSettings.Settings
            .Select(setting => ScoringHelper.SearchScoringPredicate(query, setting))
            .Where(scoredSetting => scoredSetting.Score > 0)
            .OrderByDescending(scoredSetting => scoredSetting.Score);

        if (!filteredList.Any())
        {
            return;
        }

        if (filteredList.Count() == 1 ||
            filteredList.Any(a => a.Score == 10))
        {
            var setting = filteredList.First().Setting;

            Title = setting.Name;
            Subtitle = setting.JoinedFullSettingsPath;
            Icon = Icons.WindowsSettingsIcon;
            Command = new OpenSettingsCommand(setting)
            {
                Icon = Icons.WindowsSettingsIcon,
                Name = setting.Name,
            };

            // There is a case with MMC snap-ins where we don't have .msc files fort them. Then we need to show the note for this results in subtitle too.
            // These results have mmc.exe as command and their note property is filled.
            if (setting.Command == "mmc.exe" && !string.IsNullOrEmpty(setting.Note))
            {
                Subtitle += $"\u0020\u0020\u002D\u0020\u0020{Resources.Note}: {setting.Note}"; // "\u0020\u0020\u002D\u0020\u0020" = "<space><space><minus><space><space>"
            }

            return;
        }

        // We found more than one result. Make our command take
        // us to the Windows Settings search page, prepopulated with this search.
        var settingsPage = new WindowsSettingsListPage(_windowsSettings, query);
        Title = string.Format(CultureInfo.CurrentCulture, _title, query);
        Icon = Icons.WindowsSettingsIcon;
        Subtitle = _subtitle;
        Command = settingsPage;

        return;
    }
}

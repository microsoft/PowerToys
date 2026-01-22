// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed class SettingsManager : JsonSettingsManager, ISettingOptions
{
    private const string Namespace = "clipboardHistory";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ToggleSetting _keepAfterPaste = new(
        Namespaced(nameof(KeepAfterPaste)),
        Resources.settings_keep_after_paste_title!,
        Resources.settings_keep_after_paste_description!,
        false);

    private readonly ToggleSetting _confirmDelete = new(
        Namespaced(nameof(DeleteFromHistoryRequiresConfirmation)),
        Resources.settings_confirm_delete_title!,
        Resources.settings_confirm_delete_description!,
        true);

    private readonly ChoiceSetSetting _primaryAction = new(
        Namespaced(nameof(PrimaryAction)),
        Resources.settings_primary_action_title!,
        Resources.settings_primary_action_description!,
        [
            new ChoiceSetSetting.Choice(Resources.settings_primary_action_default!, PrimaryAction.Default.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.settings_primary_action_paste!, PrimaryAction.Paste.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.settings_primary_action_copy!, PrimaryAction.Copy.ToString("G"))
        ]);

    public bool KeepAfterPaste => _keepAfterPaste.Value;

    public bool DeleteFromHistoryRequiresConfirmation => _confirmDelete.Value;

    public PrimaryAction PrimaryAction => Enum.TryParse<PrimaryAction>(_primaryAction.Value, out var action) ? action : PrimaryAction.Default;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_keepAfterPaste);
        Settings.Add(_confirmDelete);
        Settings.Add(_primaryAction);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "indexer";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _fallbackCommandModeChoice =
    [
        new ChoiceSetSetting.Choice(Resources.Indexer_Settings_FallbackCommand_AlwaysOn, ((int)FallbackCommandMode.AlwaysOn).ToString(CultureInfo.CurrentCulture)),
        new ChoiceSetSetting.Choice(Resources.Indexer_Settings_FallbackCommand_Off, ((int)FallbackCommandMode.Off).ToString(CultureInfo.CurrentCulture)),
        new ChoiceSetSetting.Choice(Resources.Indexer_Settings_FallbackCommand_FilePathExist, ((int)FallbackCommandMode.FilePathExist).ToString(CultureInfo.CurrentCulture)),
    ];

    public enum FallbackCommandMode
    {
        FilePathExist = 0,
        Off = 1,
        AlwaysOn = 2,
    }

    private readonly ChoiceSetSetting _fallbackCommandMode = new(
    Namespaced(nameof(FallbackCommandModeSettings)),
    Resources.Indexer_Settings_FallbackCommand_Mode,
    Resources.Indexer_Settings_FallbackCommand_Mode,
    _fallbackCommandModeChoice);

    public FallbackCommandMode FallbackCommandModeSettings
    {
        get
        {
            if (_fallbackCommandMode.Value == null || string.IsNullOrEmpty(_fallbackCommandMode.Value))
            {
                // default behavior
                return FallbackCommandMode.FilePathExist;
            }

            // convert _fallbackCommandMode.Value from string to FallbackCommandMode
            var success = int.TryParse(_fallbackCommandMode.Value, CultureInfo.CurrentCulture, out var parsedValue);
            if (!success)
            {
                return FallbackCommandMode.FilePathExist;
            }

            return (FallbackCommandMode)parsedValue;
        }
    }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_fallbackCommandMode);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}

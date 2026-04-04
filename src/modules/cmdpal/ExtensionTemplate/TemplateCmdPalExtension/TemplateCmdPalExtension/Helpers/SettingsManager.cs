// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace TemplateCmdPalExtension;

internal sealed partial class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "templatecmdpalextension";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    // TODO: Add your settings here. For example:
    // private readonly ToggleSetting _myToggle = new(
    //     Namespaced(nameof(MyToggle)),
    //     "My toggle setting",
    //     "Description of my toggle setting",
    //     false);

    // TODO: Add accessors for each setting. For example:
    // public bool MyToggle => _myToggle.Value;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("TemplateCmdPalExtension");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        // TODO: Register your settings here. For example:
        // Settings.Add(_myToggle);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}

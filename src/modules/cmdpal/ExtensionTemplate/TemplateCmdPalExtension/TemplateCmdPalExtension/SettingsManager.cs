// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace TemplateCmdPalExtension;

/// <summary>
/// Manages extension settings using the JsonSettingsManager base class.
/// Add your own settings as ToggleSetting, TextSetting, or ChoiceSetSetting
/// fields, register them in the constructor, and they'll automatically
/// persist to disk and appear in the extension's settings page.
/// </summary>
public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "template-extension";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    // Example toggle setting. Replace or add your own settings here.
    private readonly ToggleSetting _exampleToggle = new(
        Namespaced(nameof(ExampleEnabled)),
        "Example feature",
        "Enable or disable the example feature",
        true);

    public bool ExampleEnabled => _exampleToggle.Value;

    public SettingsPage Settings { get; }

    public SettingsManager()
    {
        Settings = new SettingsPage(
            "template-extension-settings",
            "Extension Settings",
            [_exampleToggle]);

        LoadSettings();
        Settings.SettingsChanged += (s, a) => SaveSettings();
    }
}

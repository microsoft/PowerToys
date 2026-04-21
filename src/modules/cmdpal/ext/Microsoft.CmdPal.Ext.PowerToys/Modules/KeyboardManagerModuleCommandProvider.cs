// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class KeyboardManagerModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.KBM;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (IsUseNewEditorEnabled())
        {
            yield return new ListItem(new OpenNewKeyboardManagerEditorCommand() { Id = "com.microsoft.powertoys.keyboardManager.openNewEditor" })
            {
                Title = Resources.KeyboardManager_OpenNewEditor_Title,
                Subtitle = Resources.KeyboardManager_OpenNewEditor_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.keyboardManager.openSettings" })
        {
            Title = title,
            Subtitle = Resources.KeyboardManager_Settings_Subtitle,
            Icon = icon,
        };
    }

    private static string GetResourceString(string resourceName, string fallback)
    {
        return Resources.ResourceManager.GetString(resourceName, Resources.Culture) ?? fallback;
    }

    private static bool IsUseNewEditorEnabled()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "Keyboard Manager",
                "settings.json");

            if (!File.Exists(settingsPath))
            {
                return false;
            }

            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("useNewEditor", out var useNewEditor))
            {
                return useNewEditor.GetBoolean();
            }
        }
        catch
        {
            // If we can't read the setting, default to not showing the command
        }

        return false;
    }
}

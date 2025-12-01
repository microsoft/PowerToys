// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Helpers;

internal static class PowerToysResourcesHelper
{
    public static IconInfo ProviderIcon() => IconHelpers.FromRelativePath("Assets\\PowerToys.png");

    public static IconInfo ModuleIcon(this SettingsWindow module)
    {
        var iconPath = module switch
        {
            SettingsWindow.ColorPicker => "Assets\\ColorPicker.png",
            SettingsWindow.FancyZones => "Assets\\FancyZones.png",
            SettingsWindow.Hosts => "Assets\\Hosts.png",
            SettingsWindow.PowerOCR => "Assets\\TextExtractor.png",
            SettingsWindow.RegistryPreview => "Assets\\RegistryPreview.png",
            SettingsWindow.MeasureTool => "Assets\\ScreenRuler.png",
            SettingsWindow.ShortcutGuide => "Assets\\ShortcutGuide.png",
            SettingsWindow.CropAndLock => "Assets\\CropAndLock.png",
            SettingsWindow.EnvironmentVariables => "Assets\\EnvironmentVariables.png",
            SettingsWindow.Awake => "Assets\\Awake.png",
            SettingsWindow.PowerRename => "Assets\\PowerRename.png",
            SettingsWindow.Run => "Assets\\PowerToysRun.png",
            SettingsWindow.ImageResizer => "Assets\\ImageResizer.png",
            SettingsWindow.KBM => "Assets\\KeyboardManager.png",
            SettingsWindow.MouseUtils => "Assets\\MouseUtils.png",
            SettingsWindow.Workspaces => "Assets\\Workspaces.png",
            SettingsWindow.AdvancedPaste => "Assets\\AdvancedPaste.png",
            SettingsWindow.CmdPal => "Assets\\CmdPal.png",
            SettingsWindow.ZoomIt => "Assets\\ZoomIt.png",
            SettingsWindow.FileExplorer => "Assets\\FileExplorerPreview.png",
            SettingsWindow.FileLocksmith => "Assets\\FileLocksmith.png",
            SettingsWindow.NewPlus => "Assets\\NewPlus.png",
            SettingsWindow.Peek => "Assets\\Peek.png",
            SettingsWindow.LightSwitch => "Assets\\LightSwitch.png",
            SettingsWindow.AlwaysOnTop => "Assets\\AlwaysOnTop.png",
            SettingsWindow.CmdNotFound => "Assets\\CommandNotFound.png",
            SettingsWindow.MouseWithoutBorders => "Assets\\MouseWithoutBorders.png",
            SettingsWindow.PowerAccent => "Assets\\QuickAccent.png",
            SettingsWindow.PowerLauncher => "Assets\\PowerToysRun.png",
            SettingsWindow.PowerPreview => "Assets\\FileExplorerPreview.png",
            SettingsWindow.Overview => "Assets\\PowerToys.png",
            SettingsWindow.Dashboard => "Assets\\PowerToys.png",
            _ => "Assets\\PowerToys.png",
        };

        return IconHelpers.FromRelativePath(iconPath);
    }

    public static string ModuleDisplayName(this SettingsWindow module)
    {
        return module switch
        {
            SettingsWindow.ColorPicker => "Color Picker",
            SettingsWindow.FancyZones => "FancyZones",
            SettingsWindow.Hosts => "Hosts File Editor",
            SettingsWindow.PowerOCR => "Text Extractor",
            SettingsWindow.RegistryPreview => "Registry Preview",
            SettingsWindow.MeasureTool => "Screen Ruler",
            SettingsWindow.ShortcutGuide => "Shortcut Guide",
            SettingsWindow.CropAndLock => "Crop And Lock",
            SettingsWindow.EnvironmentVariables => "Environment Variables",
            SettingsWindow.Awake => "Awake",
            SettingsWindow.PowerRename => "PowerRename",
            SettingsWindow.Run => "PowerToys Run",
            SettingsWindow.ImageResizer => "Image Resizer",
            SettingsWindow.KBM => "Keyboard Manager",
            SettingsWindow.MouseUtils => "Mouse Utilities",
            SettingsWindow.Workspaces => "Workspaces",
            SettingsWindow.AdvancedPaste => "Advanced Paste",
            SettingsWindow.CmdPal => "Command Palette",
            SettingsWindow.ZoomIt => "ZoomIt",
            SettingsWindow.FileExplorer => "File Explorer Add-ons",
            SettingsWindow.FileLocksmith => "File Locksmith",
            SettingsWindow.NewPlus => "New+",
            SettingsWindow.Peek => "Peek",
            SettingsWindow.LightSwitch => "Light Switch",
            SettingsWindow.AlwaysOnTop => "Always On Top",
            SettingsWindow.CmdNotFound => "Command Not Found",
            SettingsWindow.MouseWithoutBorders => "Mouse Without Borders",
            SettingsWindow.PowerAccent => "Quick Accent",
            SettingsWindow.Overview => "General",
            SettingsWindow.Dashboard => "Dashboard",
            SettingsWindow.PowerLauncher => "PowerToys Run",
            SettingsWindow.PowerPreview => "File Explorer Add-ons",
            _ => module.ToString(),
        };
    }
}

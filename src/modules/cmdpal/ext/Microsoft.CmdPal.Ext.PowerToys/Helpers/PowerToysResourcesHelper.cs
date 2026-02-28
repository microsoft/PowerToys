// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Helpers;

internal static class PowerToysResourcesHelper
{
    private const string SettingsIconRoot = "WinUI3Apps\\Assets\\Settings\\Icons\\";

    internal static IconInfo IconFromSettingsIcon(string fileName) => IconHelpers.FromRelativePath($"{SettingsIconRoot}{fileName}");

#if DEBUG
    public static IconInfo ProviderIcon() => IconFromSettingsIcon("PowerToys.dark.png");
#else
    public static IconInfo ProviderIcon() => IconFromSettingsIcon("PowerToys.png");
#endif

    public static IconInfo ModuleIcon(this SettingsWindow module)
    {
        var iconFile = module switch
        {
            SettingsWindow.ColorPicker => "ColorPicker.png",
            SettingsWindow.FancyZones => "FancyZones.png",
            SettingsWindow.Hosts => "Hosts.png",
            SettingsWindow.PowerOCR => "TextExtractor.png",
            SettingsWindow.RegistryPreview => "RegistryPreview.png",
            SettingsWindow.MeasureTool => "ScreenRuler.png",
            SettingsWindow.ShortcutGuide => "ShortcutGuide.png",
            SettingsWindow.CropAndLock => "CropAndLock.png",
            SettingsWindow.EnvironmentVariables => "EnvironmentVariables.png",
            SettingsWindow.Awake => "Awake.png",
            SettingsWindow.PowerRename => "PowerRename.png",
            SettingsWindow.Run => "PowerToysRun.png",
            SettingsWindow.ImageResizer => "ImageResizer.png",
            SettingsWindow.KBM => "KeyboardManager.png",
            SettingsWindow.MouseUtils => "MouseUtils.png",
            SettingsWindow.Workspaces => "Workspaces.png",
            SettingsWindow.AdvancedPaste => "AdvancedPaste.png",
            SettingsWindow.CmdPal => "CmdPal.png",
            SettingsWindow.ZoomIt => "ZoomIt.png",
            SettingsWindow.FileExplorer => "FileExplorerPreview.png",
            SettingsWindow.FileLocksmith => "FileLocksmith.png",
            SettingsWindow.NewPlus => "NewPlus.png",
            SettingsWindow.Peek => "Peek.png",
            SettingsWindow.LightSwitch => "LightSwitch.png",
            SettingsWindow.AlwaysOnTop => "AlwaysOnTop.png",
            SettingsWindow.CmdNotFound => "CommandNotFound.png",
            SettingsWindow.MouseWithoutBorders => "MouseWithoutBorders.png",
            SettingsWindow.PowerAccent => "QuickAccent.png",
            SettingsWindow.PowerLauncher => "PowerToysRun.png",
            SettingsWindow.PowerPreview => "FileExplorerPreview.png",
            SettingsWindow.Overview => "PowerToys.png",
            SettingsWindow.Dashboard => "PowerToys.png",
            _ => "PowerToys.png",
        };

        return IconFromSettingsIcon(iconFile);
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

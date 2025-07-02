// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Ext.PowerToys.Classes;
using Microsoft.CmdPal.Ext.PowerToys.Commands;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

// A helper class for module items.
internal static class ModuleItemsHelper
{
    public static ListItem? CreateCommandItem(this SettingsWindow module)
    {
        switch (module)
        {
            // These modules have their own UI
            case SettingsWindow.Workspaces:
            case SettingsWindow.FancyZones:
            case SettingsWindow.KBM:
            case SettingsWindow.Hosts:
            case SettingsWindow.EnvironmentVariables:
            case SettingsWindow.RegistryPreview:
            case SettingsWindow.ShortcutGuide:
                return new ListItem(new OpenInSettingsCommand(new PowerToysModuleEntry
                {
                    Module = module,
                }))
                {
                    Icon = module.ModuleIcon(),
                    Title = module.ModuleName(),
                };
            case SettingsWindow.Awake:
            case SettingsWindow.ColorPicker:
            case SettingsWindow.Run:
            case SettingsWindow.ImageResizer:
            case SettingsWindow.MouseUtils:
            case SettingsWindow.PowerRename:
            case SettingsWindow.FileExplorer:
            case SettingsWindow.MeasureTool:
            case SettingsWindow.PowerOCR:
            case SettingsWindow.CropAndLock:
            case SettingsWindow.AdvancedPaste:
            case SettingsWindow.CmdPal:
            case SettingsWindow.ZoomIt:
            case SettingsWindow.AlwaysOnTop:
            case SettingsWindow.FileLockSmith:
            case SettingsWindow.NewPlus:
            case SettingsWindow.Peek:
            case SettingsWindow.CommandNotFound:
            case SettingsWindow.MouseWithoutBorders:
            case SettingsWindow.QuickAccent:
                return new ListItem(new OpenInSettingsCommand(new PowerToysModuleEntry
                {
                    Module = module,
                }))
                {
                    Icon = module.ModuleIcon(),
                    Title = module.ModuleName(),
                };
            case SettingsWindow.Overview:
            case SettingsWindow.Dashboard:
                return null;
            default:
                throw new NotImplementedException();
        }
    }

    public static List<ListItem> AllItems()
    {
        var items = new List<ListItem>();
        foreach (var module in Enum.GetValues<SettingsWindow>())
        {
            var item = module.CreateCommandItem();
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }
}

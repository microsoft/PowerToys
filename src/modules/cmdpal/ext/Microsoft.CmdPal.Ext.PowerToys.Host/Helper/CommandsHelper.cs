// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerToys.Classes;
using Microsoft.CmdPal.Ext.PowerToys.Commands;
using Microsoft.CmdPal.Ext.PowerToys.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

internal static class CommandsHelper
{
    public static IList<ICommandContextItem> GetCommands(this PowerToysModuleEntry entry)
    {
        switch (entry.Module)
        {
            case SettingsWindow.ColorPicker:
                return new List<ICommandContextItem>()
                            {
                                new CommandContextItem(new ColorPickerListPage()),
                            };
            case SettingsWindow.CropAndLock:
                return new List<ICommandContextItem>()
                            {
                                new CommandContextItem(new CrockAndLockThumbnailCommand()),
                                new CommandContextItem(new CrockAndLockReparentCommand()),
                            };
            case SettingsWindow.Awake:
                return new List<ICommandContextItem>()
                            {
                                new CommandContextItem(new AwakePage()),
                            };
            case SettingsWindow.Workspaces:
                return new List<ICommandContextItem>()
                            {
                                new CommandContextItem(new WorkspacesListPage()),
                            };
            default:
                return new List<ICommandContextItem>();
        }
    }
}

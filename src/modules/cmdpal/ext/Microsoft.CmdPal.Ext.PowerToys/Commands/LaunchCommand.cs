// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CmdPal.Ext.PowerToys.Classes;
using Microsoft.CmdPal.Ext.PowerToys.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.Commands;

internal sealed partial class LaunchCommand : InvokableCommand
{
    private readonly PowerToysModuleEntry _entry;

    internal LaunchCommand(PowerToysModuleEntry entry)
    {
        _entry = entry;
        Name = Resources.Launch_CommandName;
    }

    public override CommandResult Invoke()
    {
        Launch();
        return CommandResult.KeepOpen();
    }

    public void Launch()
    {
        switch (_entry.Module)
        {
            case SettingsWindow.ColorPicker:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.FancyZones:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.ImageResizer:
                {
                    // ImageResizer doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return;
                }

            case SettingsWindow.KBM:
                {
                    // Keyboard Manager doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return;
                }

            case SettingsWindow.MouseUtils:
                {
                    // Mouse Utils doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return;
                }

            case SettingsWindow.PowerRename:
                {
                    // PowerRename doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return;
                }

            case SettingsWindow.Run:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.PowerAccent:
                {
                    // PowerAccent doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return;
                }

            case SettingsWindow.Hosts:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsSharedEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.MeasureTool:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.PowerOCR:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.ShortcutGuide:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.RegistryPreview:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.CropAndLock:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockThumbnailEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.EnvironmentVariables:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowEnvironmentVariablesSharedEvent());
                    eventHandle.Set();
                    return;
                }

            case SettingsWindow.Workspaces:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent());
                    eventHandle.Set();
                    return;
                }

            default:
                {
                    // For modules without specific launch events, open settings page
                    _entry.NavigateToSettingsPage();
                    return;
                }
        }
    }
}

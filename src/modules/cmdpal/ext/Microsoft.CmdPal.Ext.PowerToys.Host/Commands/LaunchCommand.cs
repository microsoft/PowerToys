// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CmdPal.Ext.PowerToys.Classes;
using Microsoft.CmdPal.Ext.PowerToys.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;
using static Common.UI.SettingsDeepLink;
using Workspaces.ModuleServices;

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
        switch (_entry.Module)
        {
            case SettingsWindow.ColorPicker:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.FancyZones:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.ImageResizer:
                {
                    // ImageResizer doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.KBM:
                {
                    // Keyboard Manager doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.MouseUtils:
                {
                    // Mouse Utils doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.PowerRename:
                {
                    // PowerRename doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.Run:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.PowerAccent:
                {
                    // PowerAccent doesn't have a direct launch event, open settings instead
                    _entry.NavigateToSettingsPage();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.Hosts:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsSharedEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.MeasureTool:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.PowerOCR:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.ShortcutGuide:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.RegistryPreview:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.CropAndLock:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockThumbnailEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.EnvironmentVariables:
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowEnvironmentVariablesSharedEvent());
                    eventHandle.Set();
                    return CommandResult.KeepOpen();
                }

            case SettingsWindow.Workspaces:
                {
                    var result = WorkspaceService.Instance.LaunchEditorAsync().GetAwaiter().GetResult();
                    return result.Success
                        ? CommandResult.KeepOpen()
                        : CommandResult.ShowToast(result.Error ?? "Failed to launch Workspaces editor.");
                }

            default:
                {
                    // For modules without specific launch events, open settings page
                    _entry.NavigateToSettingsPage();
                    return CommandResult.KeepOpen();
                }
        }
    }
}

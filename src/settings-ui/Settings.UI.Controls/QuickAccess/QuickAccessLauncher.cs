// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public class QuickAccessLauncher : IQuickAccessLauncher
    {
        private readonly bool _isElevated;
        private readonly Func<string, int>? _sendMessage;

        public QuickAccessLauncher(bool isElevated, Func<string, int>? sendMessage = null)
        {
            _isElevated = isElevated;
            _sendMessage = sendMessage;
        }

        public virtual bool Launch(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.ColorPicker:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.EnvironmentVariables:
                    {
                        bool launchAdmin = SettingsRepository<EnvironmentVariablesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
                        string eventName = !_isElevated && launchAdmin
                            ? Constants.ShowEnvironmentVariablesAdminSharedEvent()
                            : Constants.ShowEnvironmentVariablesSharedEvent();

                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                        {
                            eventHandle.Set();
                        }
                    }

                    return true;
                case ModuleType.FancyZones:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.Hosts:
                    {
                        bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
                        string eventName = !_isElevated && launchAdmin
                            ? Constants.ShowHostsAdminSharedEvent()
                            : Constants.ShowHostsSharedEvent();

                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                        {
                            eventHandle.Set();
                        }
                    }

                    return true;
                case ModuleType.PowerLauncher:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.PowerOCR:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.RegistryPreview:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.MeasureTool:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.ShortcutGuide:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.CmdPal:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowCmdPalEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.Workspaces:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.LightSwitch:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.LightSwitchToggleEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.PowerDisplay:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.TogglePowerDisplayEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.KeyboardManager:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.OpenNewKeyboardManagerEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.MousePointerCrosshairs:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MouseCrosshairsTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.MouseWithoutBorders:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MWBReconnectEvent()))
                    {
                        eventHandle.Set();
                    }

                    return true;
                case ModuleType.SoundManager:
                    // The Sound Manager has no shared event, so the module is asked to open its window through the runner.
                    if (_sendMessage == null)
                    {
                        return false;
                    }

                    return _sendMessage("{\"action\":{\"" + SoundManagerSettings.ModuleName + "\":{\"action_name\":\"open-sound-manager\",\"value\":\"\"}}}") == 0;
                default:
                    return false;
            }
        }
    }
}

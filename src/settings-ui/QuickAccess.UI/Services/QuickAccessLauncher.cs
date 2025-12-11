// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.Interop;

namespace Microsoft.PowerToys.QuickAccess.Services
{
    public class QuickAccessLauncher : IQuickAccessLauncher
    {
        private readonly IQuickAccessCoordinator _coordinator;

        public QuickAccessLauncher(IQuickAccessCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void Launch(ModuleType moduleType)
        {
            bool moduleRun = true;

            switch (moduleType)
            {
                case ModuleType.ColorPicker:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.EnvironmentVariables:
                    {
                        bool launchAdmin = SettingsRepository<EnvironmentVariablesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
                        bool isElevated = _coordinator?.IsRunnerElevated ?? false;
                        string eventName = !isElevated && launchAdmin
                            ? Constants.ShowEnvironmentVariablesAdminSharedEvent()
                            : Constants.ShowEnvironmentVariablesSharedEvent();

                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                        {
                            eventHandle.Set();
                        }
                    }

                    break;
                case ModuleType.FancyZones:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.Hosts:
                    {
                        bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
                        bool isElevated = _coordinator?.IsRunnerElevated ?? false;
                        string eventName = !isElevated && launchAdmin
                            ? Constants.ShowHostsAdminSharedEvent()
                            : Constants.ShowHostsSharedEvent();

                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                        {
                            eventHandle.Set();
                        }
                    }

                    break;
                case ModuleType.PowerLauncher:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.PowerOCR:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.RegistryPreview:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.MeasureTool:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.ShortcutGuide:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.CmdPal:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowCmdPalEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.Workspaces:
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                default:
                    moduleRun = false;
                    break;
            }

            if (moduleRun)
            {
                _coordinator?.OnModuleLaunched(moduleType);
            }

            _coordinator?.HideFlyout();
        }
    }
}

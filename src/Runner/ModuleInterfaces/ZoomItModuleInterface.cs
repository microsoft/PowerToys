// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class ZoomItModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleCustomActionsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "ZoomIt";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.ZoomIt;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredZoomItEnabledValue();

        public override string ProcessPath => "PowerToys.ZoomIt.exe";

        public override string ProcessName => "PowerToys.ZoomIt";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public void Disable()
        {
        }

        public void Enable()
        {
        }

        public Dictionary<string, Action<string>> CustomActions { get => new() { { "refresh_settings", (_) => OnSettingsChanged() } }; }

        public void OnSettingsChanged()
        {
            using var refreshSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ZoomItRefreshSettingsEvent());
            refreshSettingsEvent.Set();
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class PowerOCRModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "TextExtractor";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.PowerOcr;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredTextExtractorEnabledValue();

        public override string ProcessPath => "PowerToys.PowerOCR.exe";

        public override string ProcessName => "PowerToys.PowerOCR";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        private void PopulateShortcuts()
        {
            Shortcuts.Clear();
            Shortcuts.Add((SettingsUtils.Default.GetSettings<PowerOcrSettings>(Name).Properties.ActivationShortcut, () =>
                {
                    using var invokeOcrEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent());
                    invokeOcrEvent.Set();
                }
            ));
        }

        public void Disable()
        {
            using var terminateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.TerminatePowerOCRSharedEvent());
            terminateEvent.Set();
        }

        public void Enable()
        {
            PopulateShortcuts();
        }

        public void OnSettingsChanged()
        {
            PopulateShortcuts();
        }
    }
}

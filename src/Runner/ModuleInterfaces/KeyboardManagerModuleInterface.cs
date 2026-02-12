// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class KeyboardManagerModuleInterface : ProcessModuleAbstractClass, IPowerToysModule
    {
        public string Name => "Keyboard Manager";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.KeyboardManager;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredKeyboardManagerEnabledValue();

        public override string ProcessPath => "KeyboardManagerEngine\\PowerToys.KeyboardManagerEngine.exe";

        public override string ProcessName => "PowerToys.KeyboardManagerEngine";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument | ProcessLaunchOptions.RealtimePriority;

        public void Disable()
        {
            using var terminateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.TerminateKBMSharedEvent());
            terminateEvent.Set();
        }

        public void Enable()
        {
        }
    }
}

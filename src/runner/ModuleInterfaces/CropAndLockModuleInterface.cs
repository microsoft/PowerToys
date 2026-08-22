// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class CropAndLockModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IDisposable, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "CropAndLock";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.CropAndLock;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredCropAndLockEnabledValue();

        private EventWaitHandle _reparentEvent = new(false, EventResetMode.AutoReset, Constants.CropAndLockReparentEvent());
        private EventWaitHandle _thumbnailEvent = new(false, EventResetMode.AutoReset, Constants.CropAndLockThumbnailEvent());
        private EventWaitHandle _terminateEvent = new(false, EventResetMode.AutoReset, Constants.CropAndLockExitEvent());

        public void Disable()
        {
            _terminateEvent.Set();
        }

        public void Enable()
        {
            PopulateShortcuts();
        }

        public void PopulateShortcuts()
        {
            Shortcuts.Clear();
            var settings = SettingsUtils.Default.GetSettings<CropAndLockSettings>(Name);
            Shortcuts.Add((settings.Properties.ThumbnailHotkey.Value,  () => _thumbnailEvent.Set()));
            Shortcuts.Add((settings.Properties.ReparentHotkey.Value,  () => _reparentEvent.Set()));
        }

        public void OnSettingsChanged()
        {
            PopulateShortcuts();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public override string ProcessPath => "PowerToys.CropAndLock.exe";

        public override string ProcessName => "PowerToys.CropAndLock";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _reparentEvent.Dispose();
            _thumbnailEvent.Dispose();
            _terminateEvent.Dispose();
        }
    }
}

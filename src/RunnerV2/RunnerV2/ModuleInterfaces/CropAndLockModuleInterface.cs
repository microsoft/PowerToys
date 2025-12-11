// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed partial class CropAndLockModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IDisposable
    {
        public string Name => "CropAndLock";

        public bool Enabled => new SettingsUtils().GetSettings<GeneralSettings>().Enabled.CropAndLock;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredCropAndLockEnabledValue();

        private InteropEvent _reparentEvent = new(InteropEvent.CropAndLockReparent);
        private InteropEvent _thumbnailEvent = new(InteropEvent.CropAndLockThumbnail);
        private InteropEvent _terminateEvent = new(InteropEvent.CropAndLockTerminate);

        public void Disable()
        {
            _terminateEvent.Fire();
            _reparentEvent.Dispose();
            _thumbnailEvent.Dispose();
            _terminateEvent.Dispose();
        }

        public void Enable()
        {
            _reparentEvent = new(InteropEvent.CropAndLockReparent);
            _thumbnailEvent = new(InteropEvent.CropAndLockThumbnail);
            _terminateEvent = new(InteropEvent.CropAndLockTerminate);
            PopulateShortcuts();
        }

        public void PopulateShortcuts()
        {
            Shortcuts.Clear();
            var settings = new SettingsUtils().GetSettings<CropAndLockSettings>(Name);
            Shortcuts.Add((settings.Properties.ThumbnailHotkey.Value, _thumbnailEvent.Fire));
            Shortcuts.Add((settings.Properties.ReparentHotkey.Value, _reparentEvent.Fire));
        }

        public void OnSettingsChanged(string settingsKind, JsonElement jsonProperties)
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

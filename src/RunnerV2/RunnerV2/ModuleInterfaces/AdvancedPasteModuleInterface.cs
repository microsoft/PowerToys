// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class AdvancedPasteModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IDisposable, IPowerToysModuleShortcutsProvider, IPowerToysModuleSettingsChangedSubscriber
    {
        public string Name => "AdvancedPaste";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.AdvancedPaste;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAdvancedPasteEnabledValue();

        public void Disable()
        {
            if (_ipc != null)
            {
                _ipc.Send(Constants.AdvancedPasteTerminateAppMessage());
                _ipc.End();
                _ipc = null;
            }
        }

        private const string IpcName = @"\\.\pipe\PowerToys.AdvancedPaste";
        private TwoWayPipeMessageIPCManaged? _ipc;

        public void Enable()
        {
            _ipc = new TwoWayPipeMessageIPCManaged(@"\\.\pipe\PowerToys.AdvancedPast.Input", IpcName, (_) => { });
            _ipc.Start();

            PopulateShortcuts();
        }

        public void OnSettingsChanged()
        {
            PopulateShortcuts();
        }

        public void PopulateShortcuts()
        {
            _ipc ??= new TwoWayPipeMessageIPCManaged(@"\\.\pipe\PowerToys.AdvancedPast.Input", @"\\.\pipe\PowerToys.AdvancedPaste", (_) => { });

            Shortcuts.Clear();

            AdvancedPasteSettings settings = SettingsUtils.Default.GetSettingsOrDefault<AdvancedPasteSettings>(Name);
            Shortcuts.Add((settings.Properties.AdvancedPasteUIShortcut, () =>
                _ipc.Send(Constants.AdvancedPasteShowUIMessage())
            ));
            Shortcuts.Add((settings.Properties.PasteAsPlainTextShortcut, TryToPasteAsPlainText));
            Shortcuts.Add((settings.Properties.PasteAsMarkdownShortcut, () => _ipc.Send(Constants.AdvancedPasteMarkdownMessage())));
            Shortcuts.Add((settings.Properties.PasteAsJsonShortcut, () => _ipc.Send(Constants.AdvancedPasteJsonMessage())));

            HotkeyAccessor[] hotkeyAccessors = settings.GetAllHotkeyAccessors();
            int additionalActionsCount = settings.Properties.AdditionalActions.GetAllActions().Count() - 2;
            for (int i = 0; i < additionalActionsCount; i++)
            {
                int scopedI = i;
                Shortcuts.Add((hotkeyAccessors[4 + i].Value, () => _ipc.Send(Constants.AdvancedPasteAdditionalActionMessage() + " " + (3 + scopedI))));
            }

            for (int i = 4 + additionalActionsCount; i < hotkeyAccessors.Length; i++)
            {
                int scopedI = i;
                Shortcuts.Add((hotkeyAccessors[i].Value, () => _ipc.Send(Constants.AdvancedPasteCustomActionMessage() + " " + (scopedI - 4 - additionalActionsCount))));
            }
        }

        private void TryToPasteAsPlainText()
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                SendKeys.SendWait(text);
            }

            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _ipc?.Dispose();
        }

        public List<(HotkeySettings Hotkey, Action Action)> Shortcuts { get; } = [];

        public override string ProcessPath => "WinUI3Apps\\PowerToys.AdvancedPaste.exe";

        public override string ProcessName => "PowerToys.AdvancedPaste";

        public override string ProcessArguments => IpcName;

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess | ProcessLaunchOptions.RunnerProcessIdAsFirstArgument;
    }
}

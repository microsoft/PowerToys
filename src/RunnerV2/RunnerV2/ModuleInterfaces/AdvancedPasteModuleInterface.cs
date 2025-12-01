// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.GPOWrapper;
using PowerToys.Interop;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class AdvancedPasteModuleInterface : IPowerToysModule, IDisposable
    {
        public string Name => "AdvancedPaste";

        public bool Enabled => new SettingsUtils().GetSettings<GeneralSettings>().Enabled.AdvancedPaste;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredAdvancedPasteEnabledValue();

        public void Disable()
        {
            if (_ipc != null)
            {
                _ipc.Send("TerminateApp");
                _ipc.End();
                _ipc = null;
            }

            Task.Run(async () =>
            {
                await Task.Delay(500);
                foreach (var process in Process.GetProcessesByName("PowerToys.AdvancedPaste.exe"))
                {
                    process.Kill();
                }
            });
        }

        private TwoWayPipeMessageIPCManaged? _ipc;

        public void Enable()
        {
            if (Process.GetProcessesByName("PowerToys.AdvancedPaste.exe").Length > 0)
            {
                return;
            }

            string ipcName = @"\\.\pipe\PowerToys.AdvancedPaste";
            _ipc = new TwoWayPipeMessageIPCManaged(string.Empty, ipcName, (_) => { });
            _ipc.Start();

            if (Shortcuts.Count == 0)
            {
                PopulateShortcuts();
            }

            Process.Start("WinUI3Apps\\PowerToys.AdvancedPaste.exe", $"{Environment.ProcessId} {ipcName}");
        }

        public void OnSettingsChanged()
        {
            PopulateShortcuts();
        }

        public void PopulateShortcuts()
        {
            ArgumentNullException.ThrowIfNull(_ipc);

            Shortcuts.Clear();

            AdvancedPasteSettings settings = new SettingsUtils().GetSettings<AdvancedPasteSettings>();
            Shortcuts.Add((settings.Properties.AdvancedPasteUIShortcut, () =>
                _ipc.Send("ShowUI")
            ));
            Shortcuts.Add((settings.Properties.PasteAsPlainTextShortcut, TryToPasteAsPlainText));
            Shortcuts.Add((settings.Properties.PasteAsMarkdownShortcut, () => _ipc.Send("PasteMarkdown")));
            Shortcuts.Add((settings.Properties.PasteAsJsonShortcut, () => _ipc.Send("PasteJson")));

            HotkeyAccessor[] hotkeyAccessors = settings.GetAllHotkeyAccessors();
            for (int i = 4; i < hotkeyAccessors.Length; i++)
            {
                HotkeyAccessor hotkeyAccessor = hotkeyAccessors[i];
                Shortcuts.Add((hotkeyAccessor.Value, () => _ipc.Send($"CustomPaste {i}")));
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
    }
}

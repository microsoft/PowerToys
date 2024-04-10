// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Awake.Core.Models;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    public class TrayMessageFilter : IMessageFilter
    {
        private static SettingsUtils? _moduleSettings;

        private static SettingsUtils? ModuleSettings { get => _moduleSettings; set => _moduleSettings = value; }

        private static ManualResetEvent? _exitSignal;

        public TrayMessageFilter(ManualResetEvent? exitSignal)
        {
            _exitSignal = exitSignal;
            ModuleSettings = new SettingsUtils();
        }

        public bool PreFilterMessage(ref Message m)
        {
            var trayCommandsSize = Enum.GetNames(typeof(TrayCommands)).Length;

            switch (m.Msg)
            {
                case (int)Native.Constants.WM_COMMAND:
                    var targetCommandIndex = m.WParam.ToInt64() & 0xFFFF;
                    switch (targetCommandIndex)
                    {
                        case (long)TrayCommands.TC_EXIT:
                            ExitCommandHandler(_exitSignal);
                            break;
                        case (long)TrayCommands.TC_DISPLAY_SETTING:
                            DisplaySettingCommandHandler();
                            break;
                        case (long)TrayCommands.TC_MODE_INDEFINITE:
                            IndefiniteKeepAwakeCommandHandler();
                            break;
                        case (long)TrayCommands.TC_MODE_PASSIVE:
                            PassiveKeepAwakeCommandHandler();
                            break;
                        case var _ when targetCommandIndex >= trayCommandsSize:
                            // Format for the timer block:
                            // TrayCommands.TC_TIME + ZERO_BASED_INDEX_IN_SETTINGS
                            AwakeSettings settings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                            if (settings.Properties.CustomTrayTimes.Count == 0)
                            {
                                settings.Properties.CustomTrayTimes.AddRange(Manager.GetDefaultTrayOptions());
                            }

                            int index = (int)targetCommandIndex - (int)TrayCommands.TC_TIME;
                            var targetTime = settings.Properties.CustomTrayTimes.ElementAt(index).Value;
                            TimedKeepAwakeCommandHandler(targetTime);
                            break;
                    }

                    break;
            }

            return false;
        }

        private static void ExitCommandHandler(ManualResetEvent? exitSignal)
        {
            Manager.CompleteExit(0, exitSignal, true);
        }

        private static void DisplaySettingCommandHandler()
        {
            try
            {
                var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                currentSettings.Properties.KeepDisplayOn = !currentSettings.Properties.KeepDisplayOn;
                ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle display setting command: {ex.Message}");
            }
        }

        private static void TimedKeepAwakeCommandHandler(int seconds)
        {
            try
            {
                var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                var timeSpan = TimeSpan.FromSeconds(seconds);

                currentSettings.Properties.Mode = AwakeMode.TIMED;
                currentSettings.Properties.IntervalHours = (uint)timeSpan.Hours;
                currentSettings.Properties.IntervalMinutes = (uint)timeSpan.Minutes;

                ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle timed keep awake command: {ex.Message}");
            }
        }

        private static void PassiveKeepAwakeCommandHandler()
        {
            Manager.SetPassiveKeepAwakeMode();
        }

        private static void IndefiniteKeepAwakeCommandHandler()
        {
            try
            {
                var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                currentSettings.Properties.Mode = AwakeMode.INDEFINITE;
                ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle indefinite keep awake command: {ex.Message}");
            }
        }
    }
}

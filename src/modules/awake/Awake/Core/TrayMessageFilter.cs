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
        private static ManualResetEvent? _exitSignal;

        public TrayMessageFilter(ManualResetEvent? exitSignal)
        {
            _exitSignal = exitSignal;
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
                            AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                            if (settings.Properties.CustomTrayTimes.Count == 0)
                            {
                                settings.Properties.CustomTrayTimes.AddRange(Manager.GetDefaultTrayOptions());
                            }

                            int index = (int)targetCommandIndex - (int)TrayCommands.TC_TIME;
                            var targetTime = (uint)settings.Properties.CustomTrayTimes.ElementAt(index).Value;
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
            Manager.SetDisplay();
        }

        private static void TimedKeepAwakeCommandHandler(uint seconds)
        {
            Manager.SetTimedKeepAwake(seconds);
        }

        private static void PassiveKeepAwakeCommandHandler()
        {
            Manager.SetPassiveKeepAwake();
        }

        private static void IndefiniteKeepAwakeCommandHandler()
        {
            Manager.SetIndefiniteKeepAwake();
        }
    }
}

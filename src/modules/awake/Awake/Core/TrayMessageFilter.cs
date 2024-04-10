// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Awake.Core.Models;
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
            int trayCommandsSize = Enum.GetNames(typeof(TrayCommands)).Length;

            if (m.Msg == (int)Native.Constants.WM_COMMAND)
            {
                long targetCommandIndex = m.WParam.ToInt64() & 0xFFFF;

                switch (targetCommandIndex)
                {
                    case (long)TrayCommands.TC_EXIT:
                        Manager.CompleteExit(0, _exitSignal, true);
                        break;
                    case (long)TrayCommands.TC_DISPLAY_SETTING:
                        Manager.SetDisplay();
                        break;
                    case (long)TrayCommands.TC_MODE_INDEFINITE:
                        Manager.SetIndefiniteKeepAwake();
                        break;
                    case (long)TrayCommands.TC_MODE_PASSIVE:
                        Manager.SetPassiveKeepAwake();
                        break;
                    default:
                        if (targetCommandIndex >= trayCommandsSize)
                        {
                            AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                            if (settings.Properties.CustomTrayTimes.Count == 0)
                            {
                                settings.Properties.CustomTrayTimes.AddRange(Manager.GetDefaultTrayOptions());
                            }

                            int index = (int)targetCommandIndex - (int)TrayCommands.TC_TIME;
                            uint targetTime = (uint)settings.Properties.CustomTrayTimes.ElementAt(index).Value;
                            Manager.SetTimedKeepAwake(targetTime);
                        }

                        break;
                }
            }

            return false;
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Settings ZoneSettings { get; }

        public App()
        {
            ZoneSettings = new Settings();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            WaitForPowerToysRunner();

            LayoutModel foundModel = null;

            foreach (LayoutModel model in ZoneSettings.DefaultModels)
            {
                if (model.Type == Settings.ActiveZoneSetLayoutType)
                {
                    // found match
                    foundModel = model;
                    break;
                }
            }

            if (foundModel == null)
            {
                foreach (LayoutModel model in Settings.CustomModels)
                {
                    if ("{" + model.Guid.ToString().ToUpper() + "}" == Settings.ActiveZoneSetUUid.ToUpper())
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = ZoneSettings.DefaultModels[0];
            }

            foundModel.IsSelected = true;

            EditorOverlay overlay = new EditorOverlay();
            overlay.Show();
            overlay.DataContext = foundModel;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        private void WaitForPowerToysRunner()
        {
            Task.Run(() =>
            {
                const uint INFINITE = 0xFFFFFFFF;
                const uint WAIT_OBJECT_0 = 0x00000000;
                const uint ProcessAccessFlagSynchronize = 0x00100000;

                IntPtr powerToysProcHandle = OpenProcess(ProcessAccessFlagSynchronize, false, Settings.PowerToysPID);
                if (WaitForSingleObject(powerToysProcHandle, INFINITE) == WAIT_OBJECT_0)
                {
                    Environment.Exit(0);
                }
            });
        }
    }
}

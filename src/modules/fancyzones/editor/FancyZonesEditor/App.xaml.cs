// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Settings[] ZoneSettings { get; }

        public static int NumMonitors { get; private set; }

        public App()
        {
            NumMonitors = Environment.GetCommandLineArgs().Length / 6;
            ZoneSettings = new Settings[NumMonitors];
            for (int monitor_shift = 0; monitor_shift < NumMonitors; monitor_shift++)
            {
                ZoneSettings[monitor_shift] = new Settings(monitor_shift);
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            LayoutModel foundModel = null;

            foreach (LayoutModel model in ZoneSettings[0].DefaultModels)
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
                foundModel = ZoneSettings[0].DefaultModels[0];
            }

            foundModel.IsSelected = true;

            EditorOverlay overlay = new EditorOverlay();
            overlay.Show();
            overlay.DataContext = foundModel;
        }
    }
}

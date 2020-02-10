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
        public Settings ZoneSettings { get; }

        public App()
        {
            ZoneSettings = new Settings();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
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
    }
}

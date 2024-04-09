// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Windows.Data.Json;
using WinUIEx;

namespace RegistryPreview
{
    public sealed partial class MainWindow : WindowEx
    {
        /// <summary>
        /// Event handler to grab the main window's size and position before it closes
        /// </summary>
        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            jsonWindowPlacement.SetNamedValue("appWindow.Position.X", JsonValue.CreateNumberValue(appWindow.Position.X));
            jsonWindowPlacement.SetNamedValue("appWindow.Position.Y", JsonValue.CreateNumberValue(appWindow.Position.Y));
            jsonWindowPlacement.SetNamedValue("appWindow.Size.Width", JsonValue.CreateNumberValue(appWindow.Size.Width));
            jsonWindowPlacement.SetNamedValue("appWindow.Size.Height", JsonValue.CreateNumberValue(appWindow.Size.Height));
        }

        /// <summary>
        /// Event that is will prevent the app from closing if the "save file" flag is active
        /// </summary>
        public void Window_Closed(object sender, WindowEventArgs args)
        {
            // Save window placement
            SaveWindowPlacementFile(settingsFolder, windowPlacementFile);
        }
    }
}

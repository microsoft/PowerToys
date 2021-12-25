// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using FancyZonesEditor.Logs;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class EditorWindow : Window
    {
        protected void OnSaveApplyTemplate(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();
            var mainEditor = App.Overlay;
            if (mainEditor.CurrentDataContext is LayoutModel model)
            {
                // If new custom Canvas layout is created (i.e. edited Blank layout),
                // it's type needs to be updated
                if (model.Type == LayoutType.Blank)
                {
                    model.Type = LayoutType.Custom;
                }

                model.Persist();

                MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;
                settings.SetAppliedModel(model);
                App.Overlay.SetLayoutSettings(App.Overlay.Monitors[App.Overlay.CurrentDesktop], model);
            }

            App.FancyZonesEditorIO.SerializeZoneSettings();

            Close();
        }

        protected void OnClosed(object sender, EventArgs e)
        {
            App.Overlay.CloseEditor();
        }

        protected void OnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

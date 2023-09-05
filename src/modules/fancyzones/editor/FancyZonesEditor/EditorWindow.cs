// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using FancyZonesEditor.Models;
using ManagedCommon;

namespace FancyZonesEditor
{
    public class EditorWindow : Window
    {
        public LayoutModel EditingLayout { get; set; }

        public EditorWindow(LayoutModel editingLayout)
        {
            EditingLayout = editingLayout;
        }

        protected void OnSaveApplyTemplate(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            // If new custom Canvas layout is created (i.e. edited Blank layout),
            // it's type needs to be updated
            if (EditingLayout.Type == LayoutType.Blank)
            {
                EditingLayout.Type = LayoutType.Custom;
            }

            EditingLayout.Persist();

            MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;
            settings.SetAppliedModel(EditingLayout);
            App.Overlay.Monitors[App.Overlay.CurrentDesktop].SetLayoutSettings(EditingLayout);

            App.FancyZonesEditorIO.SerializeLayoutTemplates();
            App.FancyZonesEditorIO.SerializeCustomLayouts();
            App.FancyZonesEditorIO.SerializeAppliedLayouts();
            App.FancyZonesEditorIO.SerializeDefaultLayouts();
            App.FancyZonesEditorIO.SerializeLayoutHotkeys();

            Close();
        }

        protected void OnClosed(object sender, EventArgs e)
        {
            App.Overlay.CloseEditor();
        }

        protected void OnCancel(object sender, RoutedEventArgs e)
        {
            // restore backup, clean up
            App.Overlay.EndEditing(EditingLayout);

            // select and draw applied layout
            var settings = ((App)Application.Current).MainWindowSettings;
            settings.SetSelectedModel(settings.AppliedModel);
            App.Overlay.CurrentDataContext = settings.AppliedModel;

            Close();
        }
    }
}

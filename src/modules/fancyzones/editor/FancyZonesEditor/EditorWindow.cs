// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using FancyZonesEditor.Models;
using MahApps.Metro.Controls;

namespace FancyZonesEditor
{
    public class EditorWindow : MetroWindow
    {
        protected void OnSaveApplyTemplate(object sender, RoutedEventArgs e)
        {
            EditorOverlay mainEditor = EditorOverlay.Current;
            if (mainEditor.DataContext is LayoutModel model)
            {
                model.Persist();
            }

            LayoutModel.SerializeDeletedCustomZoneSets();

            _backToLayoutPicker = false;
            Close();
            EditorOverlay.Current.Close();
        }

        protected void OnClosed(object sender, EventArgs e)
        {
            if (_backToLayoutPicker)
            {
                EditorOverlay.Current.ShowLayoutPicker();
            }
        }

        protected void OnCancel(object sender, RoutedEventArgs e)
        {
            _backToLayoutPicker = true;
            Close();
        }

        private bool _backToLayoutPicker = true;
    }
}

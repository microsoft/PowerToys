// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Windows;
using ColorPicker.ViewModelContracts;

namespace ColorPicker.Helpers
{
    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        private readonly IColorEditorViewModel _colorEditorViewModel;
        private ColorEditorWindow _colorEditorWindow;
        private bool _colorPickerShown;
        private object _colorPickerVisibilityLock = new object();

        [ImportingConstructor]
        public AppStateHandler(IColorEditorViewModel colorEditorViewModel)
        {
            Application.Current.MainWindow.Closed += MainWindow_Closed;
            _colorEditorViewModel = colorEditorViewModel;
        }

        public event EventHandler AppShown;

        public event EventHandler AppHidden;

        public event EventHandler AppClosed;

        public void ShowColorPicker()
        {
            lock (_colorPickerVisibilityLock)
            {
                if (!_colorPickerShown)
                {
                    AppShown?.Invoke(this, EventArgs.Empty);
                    Application.Current.MainWindow.Opacity = 0;
                    Application.Current.MainWindow.Visibility = Visibility.Visible;
                    _colorPickerShown = true;
                }
            }
        }

        public void HideColorPicker()
        {
            lock (_colorPickerVisibilityLock)
            {
                if (_colorPickerShown)
                {
                    Application.Current.MainWindow.Opacity = 0;
                    Application.Current.MainWindow.Visibility = Visibility.Collapsed;
                    AppHidden?.Invoke(this, EventArgs.Empty);
                    _colorPickerShown = false;
                }
            }
        }

        public void ShowColorPickerEditor()
        {
            if (_colorEditorWindow == null)
            {
                _colorEditorWindow = new ColorEditorWindow();
                _colorEditorWindow.Content = _colorEditorViewModel;
                _colorEditorViewModel.OpenColorPickerRequested += ColorEditorViewModel_OpenColorPickerRequested;
            }

            _colorEditorViewModel.Initialize();
            _colorEditorWindow.Show();
        }

        public void HideColorPickerEditor()
        {
            if (_colorEditorWindow != null)
            {
                _colorEditorWindow.Hide();
            }
        }

        public bool IsColorPickerEditorVisible()
        {
            if (_colorEditorWindow != null)
            {
                // Check if we are visible and on top. Using focus producing unreliable results the first time the picker is opened.
                return _colorEditorWindow.Topmost && _colorEditorWindow.IsVisible;
            }

            return false;
        }

        public static void SetTopMost()
        {
            Application.Current.MainWindow.Topmost = false;
            Application.Current.MainWindow.Topmost = true;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            AppClosed?.Invoke(this, EventArgs.Empty);
        }

        private void ColorEditorViewModel_OpenColorPickerRequested(object sender, EventArgs e)
        {
            ShowColorPicker();
            _colorEditorWindow.Hide();
        }
    }
}

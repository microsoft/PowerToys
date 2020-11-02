// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for windowEditor.xaml
    /// </summary>
    public partial class CanvasEditorWindow : EditorWindow
    {
        public CanvasEditorWindow()
        {
            InitializeComponent();

            KeyUp += CanvasEditorWindow_KeyUp;

            _model = EditorOverlay.Current.DataContext as CanvasLayoutModel;
            _stashedModel = (CanvasLayoutModel)_model.Clone();
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            if (_offset + (int)(Settings.WorkArea.Width * 0.4) < (int)Settings.WorkArea.Width
                && _offset + (int)(Settings.WorkArea.Height * 0.4) < (int)Settings.WorkArea.Height)
            {
                _model.AddZone(new Int32Rect(_offset, _offset, (int)(Settings.WorkArea.Width * 0.4), (int)(Settings.WorkArea.Height * 0.4)));
            }
            else
            {
                _offset = 100;
                _model.AddZone(new Int32Rect(_offset, _offset, (int)(Settings.WorkArea.Width * 0.4), (int)(Settings.WorkArea.Height * 0.4)));
            }

            _offset += 50;
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
            _stashedModel.RestoreTo(_model);
        }

        private void CanvasEditorWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCancel(sender, null);
            }
        }

        private int _offset = 100;
        private CanvasLayoutModel _model;
        private CanvasLayoutModel _stashedModel;
    }
}

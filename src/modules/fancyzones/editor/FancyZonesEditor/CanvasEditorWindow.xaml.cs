// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
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
            _model = EditorOverlay.Current.DataContext as CanvasLayoutModel;
            _stashedModel = (CanvasLayoutModel)_model.Clone();
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            if (_x_offset + ((int)(Settings.WorkArea.Width * 0.4) / 2) < (int)Settings.WorkArea.Width
                && _y_offset + ((int)(Settings.WorkArea.Height * 0.4) / 2) < (int)Settings.WorkArea.Height)
            {
                _model.AddZone(new Int32Rect(_x_offset, _y_offset, (int)(Settings.WorkArea.Width * 0.4), (int)(Settings.WorkArea.Height * 0.4)));
            }
            else
            {
                _x_offset = 100;
                _y_offset = 100;
                _model.AddZone(new Int32Rect(_x_offset, _y_offset, (int)(Settings.WorkArea.Width * 0.4), (int)(Settings.WorkArea.Height * 0.4)));
            }

            _x_offset += 50;
            _y_offset += 50;
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
            _stashedModel.RestoreTo(_model);
        }

        private int _x_offset = 100;
        private int _y_offset = 100;
        private CanvasLayoutModel _model;
        private CanvasLayoutModel _stashedModel;
    }
}

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
            _model.AddZone(new Int32Rect(_offset, _offset, (int)(_model.ReferenceWidth * 0.6), (int)(_model.ReferenceHeight * 0.6)));
            _offset += 100;
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
            _stashedModel.RestoreTo(_model);
        }

        private int _offset = 100;
        private CanvasLayoutModel _model;
        private CanvasLayoutModel _stashedModel;
    }
}

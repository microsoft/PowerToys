// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public partial class CanvasEditorWindow : EditorWindow
    {
        // Default distance from the top and left borders to the zone.
        private const int DefaultOffset = 100;

        // Next created zone will be by OffsetShift value below and to the right of the previous.
        private const int OffsetShift = 50;

        // Zone size depends on the work area size multiplied by ZoneSizeMultiplier value.
        private const double ZoneSizeMultiplier = 0.4;

        // Distance from the top and left borders to the zone.
        private int _offset = DefaultOffset;

        private CanvasLayoutModel _model;
        private CanvasLayoutModel _stashedModel;

        public CanvasEditorWindow()
        {
            InitializeComponent();

            KeyUp += CanvasEditorWindow_KeyUp;

            _model = App.Overlay.CurrentDataContext as CanvasLayoutModel;
            _stashedModel = (CanvasLayoutModel)_model.Clone();
        }

        public LayoutModel Model
        {
            get
            {
                return _model;
            }
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            Rect workingArea = App.Overlay.WorkArea;
            int offset = (int)App.Overlay.ScaleCoordinateWithCurrentMonitorDpi(_offset);

            if (offset + (int)(workingArea.Width * ZoneSizeMultiplier) < (int)workingArea.Width
                && offset + (int)(workingArea.Height * ZoneSizeMultiplier) < (int)workingArea.Height)
            {
                _model.AddZone(new Int32Rect(offset, offset, (int)(workingArea.Width * ZoneSizeMultiplier), (int)(workingArea.Height * ZoneSizeMultiplier)));
            }
            else
            {
                _offset = DefaultOffset;
                offset = (int)App.Overlay.ScaleCoordinateWithCurrentMonitorDpi(_offset);
                _model.AddZone(new Int32Rect(offset, offset, (int)(workingArea.Width * ZoneSizeMultiplier), (int)(workingArea.Height * ZoneSizeMultiplier)));
            }

            _offset += OffsetShift;
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
    }
}

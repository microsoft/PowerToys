// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for CanvasEditor.xaml
    /// </summary>
    public partial class CanvasEditor : UserControl
    {
        // Non-localizable strings
        private const string PropertyUpdateLayoutID = "UpdateLayout";

        private CanvasLayoutModel _model;

        public CanvasEditor(CanvasLayoutModel layout)
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            KeyDown += CanvasEditor_KeyDown;
            _model = layout;
        }

        private void CanvasEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                e.Handled = true;
                App.Overlay.FocusEditorWindow();
            }
        }

        public void FocusZone()
        {
            if (Preview.Children.Count > 0)
            {
                var canvas = Preview.Children[0] as CanvasZone;
                canvas.FocusZone();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateZoneRects();
            _model.PropertyChanged += OnModelChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_model != null)
            {
                _model.PropertyChanged -= OnModelChanged;
            }
        }

        private void OnModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == PropertyUpdateLayoutID)
            {
                UpdateZoneRects();
            }
        }

        private void UpdateZoneRects()
        {
            var workArea = App.Overlay.WorkArea;
            Preview.Width = workArea.Width;
            Preview.Height = workArea.Height;

            _model.ScaleLayout(workAreaWidth: workArea.Width, workAreaHeight: workArea.Height);

            UIElementCollection previewChildren = Preview.Children;
            int previewChildrenCount = previewChildren.Count;
            while (previewChildrenCount < _model.Zones.Count)
            {
                CanvasZone zone = new CanvasZone
                {
                    Model = _model,
                };

                Preview.Children.Add(zone);
                previewChildrenCount++;
            }

            while (previewChildrenCount > _model.Zones.Count)
            {
                Preview.Children.RemoveAt(previewChildrenCount - 1);
                previewChildrenCount--;
            }

            for (int i = 0; i < previewChildrenCount; i++)
            {
                Int32Rect rect = _model.Zones[i];
                CanvasZone zone = previewChildren[i] as CanvasZone;

                zone.ZoneIndex = i;
                Canvas.SetLeft(zone, rect.X);
                Canvas.SetTop(zone, rect.Y);
                zone.Height = rect.Height;
                zone.Width = rect.Width;
                zone.LabelID.Content = i + 1;
            }
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for CanvasEditor.xaml
    /// </summary>
    public sealed partial class CanvasEditor : UserControl
    {
        // Non-localizable strings
        private const string PropertyUpdateLayoutID = "UpdateLayout";

        private readonly CanvasLayoutModel _model;

        public CanvasEditor(CanvasLayoutModel layout)
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            KeyDown += CanvasEditor_KeyDown;
            _model = layout;
        }

        public void FocusZone()
        {
            if (Preview.Children.Count > 0 && Preview.Children[0] is CanvasZone canvas)
            {
                canvas.FocusZone();
            }
        }

        private static bool IsCtrlKeyDown()
        {
            return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        }

        private void CanvasEditor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab && IsCtrlKeyDown())
            {
                e.Handled = true;
                App.Overlay.FocusEditorWindow();
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
                RectInt32 rect = _model.Zones[i];
                CanvasZone zone = previewChildren[i] as CanvasZone;

                zone.ZoneIndex = i;
                Canvas.SetLeft(zone, rect.X);
                Canvas.SetTop(zone, rect.Y);
                zone.Height = rect.Height;
                zone.Width = rect.Width;
                zone.LabelID.Text = (i + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
            }
        }
    }
}

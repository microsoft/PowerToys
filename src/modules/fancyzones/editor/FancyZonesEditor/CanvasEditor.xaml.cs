// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for CanvasEditor.xaml
    /// </summary>
    public partial class CanvasEditor : UserControl
    {
        private CanvasLayoutModel _model;

        public CanvasEditor()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CanvasLayoutModel model = (CanvasLayoutModel)DataContext;
            if (model != null)
            {
                _model = model;
                UpdateZoneRects();

                model.PropertyChanged += OnModelChanged;
            }
        }

        private void OnModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Zones")
            {
                UpdateZoneRects();
            }
        }

        private void UpdateZoneRects()
        {
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

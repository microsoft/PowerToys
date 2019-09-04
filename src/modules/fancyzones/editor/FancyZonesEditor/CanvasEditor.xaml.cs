using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for CanvasEditor.xaml
    /// </summary>
    public partial class CanvasEditor : UserControl
    {
        public CanvasEditor()
        {
            InitializeComponent();
            Loaded += CanvasEditor_Loaded;
        }

        private void CanvasEditor_Loaded(object sender, RoutedEventArgs e)
        {
            CanvasLayoutModel model = (CanvasLayoutModel)DataContext;
            if (model != null)
            {
                Model = model;
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
            while (previewChildrenCount < Model.Zones.Count)
            {
                CanvasZone zone = new CanvasZone();
                zone.Model = Model;
                Preview.Children.Add(zone);
                previewChildrenCount++;
            }

            for (int i = 0; i < previewChildrenCount; i++)
            {
                Int32Rect rect = Model.Zones[i];
                CanvasZone zone = previewChildren[i] as CanvasZone;

                zone.ZoneIndex = i;
                Canvas.SetLeft(zone, rect.X);
                Canvas.SetTop(zone, rect.Y);
                zone.MinHeight = rect.Height;
                zone.MinWidth = rect.Width;
            }
        }

        public CanvasLayoutModel Model;
    }
}

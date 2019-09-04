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
    /// Once you've "Committ"ed the starter grid, then the Zones within the grid come to life for you to be able to further subdivide them 
    /// using splitters
    /// </summary>

    public partial class CanvasZone : UserControl
    {
        public CanvasZone()
        {
            InitializeComponent();
            Canvas.SetZIndex(this, c_zIndex++);
        }

        public CanvasLayoutModel Model;
        public int ZoneIndex;

        private Point Move(double xDelta, double yDelta)
        {
            Int32Rect rect = Model.Zones[ZoneIndex];
            if (xDelta < 0)
            {
                xDelta = Math.Max(xDelta, -rect.X);
            }
            else if (xDelta > 0)
            {
                xDelta = Math.Min(xDelta, c_workArea.Width - rect.Width - rect.X);
            }

            if (yDelta < 0)
            {
                yDelta = Math.Max(yDelta, -rect.Y);
            }
            else if (yDelta > 0)
            {
                yDelta = Math.Min(yDelta, c_workArea.Height - rect.Height - rect.Y);
            }

            rect.X += (int) xDelta;
            rect.Y += (int) yDelta;

            Canvas.SetLeft(this, rect.X);
            Canvas.SetTop(this, rect.Y);
            Model.Zones[ZoneIndex] = rect;
            return new Point(xDelta, yDelta);
        }

        private void Size(double xDelta, double yDelta)
        {
            Int32Rect rect = Model.Zones[ZoneIndex];
            if (xDelta != 0)
            {
                int newWidth = rect.Width + (int) xDelta;

                if (newWidth < 48)
                {
                    newWidth = 48;
                }
                else if (newWidth > (c_workArea.Width - rect.X))
                {
                    newWidth = (int) c_workArea.Width - rect.X;
                }
                MinWidth = rect.Width = newWidth;
            }

            if (yDelta != 0)
            {
                int newHeight = rect.Height + (int)yDelta;

                if (newHeight < 48)
                {
                    newHeight = 48;
                }
                else if (newHeight > (c_workArea.Height - rect.Y))
                {
                    newHeight = (int)c_workArea.Height - rect.Y;
                }
                MinHeight = rect.Height = newHeight;
            }
            Model.Zones[ZoneIndex] = rect;
        }

        private static int c_zIndex = 0;

        // TODO: multimon
        // This needs to be the work area of the monitor we get launched on
        private static Rect c_workArea = System.Windows.SystemParameters.WorkArea;

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            Canvas.SetZIndex(this, c_zIndex++);
            base.OnPreviewMouseDown(e);
        }
        private void NWResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point actualChange = Move(e.HorizontalChange, e.VerticalChange);
            Size(-actualChange.X, -actualChange.Y);
        }

        private void NEResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point actualChange = Move(0, e.VerticalChange);
            Size(e.HorizontalChange, -actualChange.Y);
        }

        private void SWResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point actualChange = Move(e.HorizontalChange, 0);
            Size(-actualChange.X, e.VerticalChange);
        }

        private void SEResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Size(e.HorizontalChange, e.VerticalChange);
        }

        private void NResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point actualChange = Move(0, e.VerticalChange);
            Size(0, -actualChange.Y);
        }

        private void SResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Size(0, e.VerticalChange);
        }

        private void WResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point actualChange = Move(e.HorizontalChange, 0);
            Size(-actualChange.X, 0);
        }

        private void EResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Size(e.HorizontalChange, 0);
        }

        private void Caption_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Move(e.HorizontalChange, e.VerticalChange);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            ((Panel)Parent).Children.Remove(this);
            Model.RemoveZoneAt(ZoneIndex);
        }
    }
}

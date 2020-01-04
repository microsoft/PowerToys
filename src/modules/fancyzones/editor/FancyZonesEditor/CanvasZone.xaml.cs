// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Once you've "Committ"ed the starter grid, then the Zones within the grid come to life for you to be able to further subdivide them
    /// using splitters
    /// </summary>
    public partial class CanvasZone : UserControl
    {
        public CanvasLayoutModel Model { get; set; }

        public int ZoneIndex { get; set; }

        private readonly Settings _settings = ((App)Application.Current).ZoneSettings;

        private static readonly int _minZoneWidth = 64;
        private static readonly int _minZoneHeight = 72;
        private static int _zIndex = 0;

        public CanvasZone()
        {
            InitializeComponent();
            Panel.SetZIndex(this, _zIndex++);
        }

        private void Move(double xDelta, double yDelta)
        {
            Int32Rect rect = Model.Zones[ZoneIndex];
            if (xDelta < 0)
            {
                xDelta = Math.Max(xDelta, -rect.X);
            }
            else if (xDelta > 0)
            {
                xDelta = Math.Min(xDelta, _settings.WorkArea.Width - rect.Width - rect.X);
            }

            if (yDelta < 0)
            {
                yDelta = Math.Max(yDelta, -rect.Y);
            }
            else if (yDelta > 0)
            {
                yDelta = Math.Min(yDelta, _settings.WorkArea.Height - rect.Height - rect.Y);
            }

            rect.X += (int)xDelta;
            rect.Y += (int)yDelta;

            Canvas.SetLeft(this, rect.X);
            Canvas.SetTop(this, rect.Y);
            Model.Zones[ZoneIndex] = rect;
        }

        private void SizeMove(double xDelta, double yDelta)
        {
            Int32Rect rect = Model.Zones[ZoneIndex];
            if (xDelta < 0)
            {
                if ((rect.X + xDelta) < 0)
                {
                    xDelta = -rect.X;
                }
            }
            else if (xDelta > 0)
            {
                if ((rect.Width - (int)xDelta) < _minZoneWidth)
                {
                    xDelta = rect.Width - _minZoneWidth;
                }
            }

            if (yDelta < 0)
            {
                if ((rect.Y + yDelta) < 0)
                {
                    yDelta = -rect.Y;
                }
            }
            else if (yDelta > 0)
            {
                if ((rect.Height - (int)yDelta) < _minZoneHeight)
                {
                    yDelta = rect.Height - _minZoneHeight;
                }
            }

            rect.X += (int)xDelta;
            rect.Width -= (int)xDelta;
            MinWidth = rect.Width;

            rect.Y += (int)yDelta;
            rect.Height -= (int)yDelta;
            MinHeight = rect.Height;

            Canvas.SetLeft(this, rect.X);
            Canvas.SetTop(this, rect.Y);
            Model.Zones[ZoneIndex] = rect;
        }

        private void Size(double xDelta, double yDelta)
        {
            Int32Rect rect = Model.Zones[ZoneIndex];
            if (xDelta != 0)
            {
                int newWidth = rect.Width + (int)xDelta;

                if (newWidth < _minZoneWidth)
                {
                    newWidth = _minZoneWidth;
                }
                else if (newWidth > (_settings.WorkArea.Width - rect.X))
                {
                    newWidth = (int)_settings.WorkArea.Width - rect.X;
                }

                MinWidth = rect.Width = newWidth;
            }

            if (yDelta != 0)
            {
                int newHeight = rect.Height + (int)yDelta;

                if (newHeight < _minZoneHeight)
                {
                    newHeight = _minZoneHeight;
                }
                else if (newHeight > (_settings.WorkArea.Height - rect.Y))
                {
                    newHeight = (int)_settings.WorkArea.Height - rect.Y;
                }

                MinHeight = rect.Height = newHeight;
            }

            Model.Zones[ZoneIndex] = rect;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            Panel.SetZIndex(this, _zIndex++);
            base.OnPreviewMouseDown(e);
        }

        private void NWResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            SizeMove(e.HorizontalChange, e.VerticalChange);
        }

        private void NEResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            SizeMove(0, e.VerticalChange);
            Size(e.HorizontalChange, 0);
        }

        private void SWResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            SizeMove(e.HorizontalChange, 0);
            Size(0, e.VerticalChange);
        }

        private void SEResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Size(e.HorizontalChange, e.VerticalChange);
        }

        private void NResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            SizeMove(0, e.VerticalChange);
        }

        private void SResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Size(0, e.VerticalChange);
        }

        private void WResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            SizeMove(e.HorizontalChange, 0);
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

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        public CanvasZone()
        {
            InitializeComponent();
            Canvas.SetZIndex(this, zIndex++);
        }

        private CanvasLayoutModel model;

        private readonly Settings _settings = ((App)Application.Current).ZoneSettings;

        private int zoneIndex;

        public enum ResizeMode
        {
            BottomEdge,
            TopEdge,
            BothEdges,
        }

        private abstract class SnappyHelperBase
        {
            public int ScreenW { get; private set; }

            protected List<int> Snaps { get; private set; }

            protected int MinValue { get; private set; }

            protected int MaxValue { get; private set; }

            public int Position { get; protected set; }

            public ResizeMode Mode { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="SnappyHelperBase"/> class.
            /// Just pass it the canvas arguments. Use mode
            /// to tell it which edges of the existing masks to use when building its list
            /// of snap points, and generally which edges to track. There will be two
            /// SnappyHelpers, one for X-coordinates and one for
            /// Y-coordinates, they work independently but share the same logic.
            /// </summary>
            /// <param name="zones">The list of rectangles describing all zones</param>
            /// <param name="zoneIndex">The index of the zone to track</param>
            /// <param name="isX"> Whether this is the X or Y SnappyHelper</param>
            /// <param name="mode"> One of the three modes of operation (for example: tracking left/right/both edges)</param>
            /// <param name="screenAxisSize"> The size of the screen in this (X or Y) dimension</param>
            public SnappyHelperBase(IList<Int32Rect> zones, int zoneIndex, bool isX, ResizeMode mode, int screenAxisSize)
            {
                int zone_position = isX ? zones[zoneIndex].X : zones[zoneIndex].Y;
                int zone_axis_size = isX ? zones[zoneIndex].Width : zones[zoneIndex].Height;
                int min_axis_size = isX ? MinZoneWidth : MinZoneHeight;
                List<int> key_positions = new List<int>();
                for (int i = 0; i < zones.Count; ++i)
                {
                    if (i != zoneIndex)
                    {
                        int ith_zone_position = isX ? zones[i].X : zones[i].Y;
                        int ith_zone_axis_size = isX ? zones[i].Width : zones[i].Height;
                        key_positions.Add(ith_zone_position);
                        key_positions.Add(ith_zone_position + ith_zone_axis_size);
                        if (mode == ResizeMode.BothEdges)
                        {
                            key_positions.Add(ith_zone_position - zone_axis_size);
                            key_positions.Add(ith_zone_position + ith_zone_axis_size - zone_axis_size);
                        }
                    }
                }

                // Remove duplicates and sort
                key_positions.Sort();
                Snaps = new List<int>();
                if (key_positions.Count > 0)
                {
                    Snaps.Add(key_positions[0]);
                    for (int i = 1; i < key_positions.Count; ++i)
                    {
                        if (key_positions[i] != key_positions[i - 1])
                        {
                            Snaps.Add(key_positions[i]);
                        }
                    }
                }

                switch (mode)
                {
                    case ResizeMode.BottomEdge:
                        // We're dragging the low edge, don't go below zero
                        MinValue = 0;

                        // It can't make the zone smaller than min_axis_size
                        MaxValue = zone_position + zone_axis_size - min_axis_size;
                        Position = zone_position;
                        break;
                    case ResizeMode.TopEdge:
                        // We're dragging the high edge, don't make the zone smaller than min_axis_size
                        MinValue = zone_position + min_axis_size;

                        // Don't go off the screen
                        MaxValue = screenAxisSize;
                        Position = zone_position + zone_axis_size;
                        break;
                    case ResizeMode.BothEdges:
                        // We're moving the window, don't move it below zero
                        MinValue = 0;

                        // Don't go off the screen (this time the lower edge is tracked)
                        MaxValue = screenAxisSize - zone_axis_size;
                        Position = zone_position;
                        break;
                }

                Mode = mode;
                this.ScreenW = screenAxisSize;
            }

            public abstract void Move(int delta);
        }

        private class SnappyHelperMagnetic : SnappyHelperBase
        {
            private List<int> magnetZoneSizes;
            private int freePosition;

            private int MagnetZoneMaxSize
            {
                get => (int)(0.08 * ScreenW);
            }

            public SnappyHelperMagnetic(IList<Int32Rect> zones, int zoneIndex, bool isX, ResizeMode mode, int screenAxisSize)
                : base(zones, zoneIndex, isX, mode, screenAxisSize)
            {
                freePosition = Position;
                magnetZoneSizes = new List<int>();
                for (int i = 0; i < Snaps.Count; ++i)
                {
                    int previous = i == 0 ? 0 : Snaps[i - 1];
                    int next = i == Snaps.Count - 1 ? ScreenW : Snaps[i + 1];
                    magnetZoneSizes.Add(Math.Min(Snaps[i] - previous, Math.Min(next - Snaps[i], MagnetZoneMaxSize)) / 2);
                }
            }

            public override void Move(int delta)
            {
                freePosition = Position + delta;
                int snapId = -1;
                for (int i = 0; i < Snaps.Count; ++i)
                {
                    if (Math.Abs(freePosition - Snaps[i]) <= magnetZoneSizes[i])
                    {
                        snapId = i;
                        break;
                    }
                }

                if (snapId == -1)
                {
                    Position = freePosition;
                }
                else
                {
                    int deadZoneWidth = (magnetZoneSizes[snapId] + 1) / 2;
                    if (Math.Abs(freePosition - Snaps[snapId]) <= deadZoneWidth)
                    {
                        Position = Snaps[snapId];
                    }
                    else if (freePosition < Snaps[snapId])
                    {
                        Position = freePosition + (freePosition - (Snaps[snapId] - magnetZoneSizes[snapId]));
                    }
                    else
                    {
                        Position = freePosition - ((Snaps[snapId] + magnetZoneSizes[snapId]) - freePosition);
                    }
                }

                Position = Math.Max(Math.Min(MaxValue, Position), MinValue);
            }
        }

        private SnappyHelperBase snappyX;
        private SnappyHelperBase snappyY;

        private SnappyHelperBase NewDefaultSnappyHelper(bool isX, ResizeMode mode, int screenAxisSize)
        {
            return new SnappyHelperMagnetic(Model.Zones, ZoneIndex, isX, mode, screenAxisSize);
        }

        private void UpdateFromSnappyHelpers()
        {
            Int32Rect rect = Model.Zones[ZoneIndex];

            if (snappyX != null)
            {
                if (snappyX.Mode == ResizeMode.BottomEdge)
                {
                    int changeX = snappyX.Position - rect.X;
                    rect.X += changeX;
                    rect.Width -= changeX;
                }
                else if (snappyX.Mode == ResizeMode.TopEdge)
                {
                    rect.Width = snappyX.Position - rect.X;
                }
                else
                {
                    int changeX = snappyX.Position - rect.X;
                    rect.X += changeX;
                }

                Canvas.SetLeft(this, rect.X);
                Width = rect.Width;
            }

            if (snappyY != null)
            {
                if (snappyY.Mode == ResizeMode.BottomEdge)
                {
                    int changeY = snappyY.Position - rect.Y;
                    rect.Y += changeY;
                    rect.Height -= changeY;
                }
                else if (snappyY.Mode == ResizeMode.TopEdge)
                {
                    rect.Height = snappyY.Position - rect.Y;
                }
                else
                {
                    int changeY = snappyY.Position - rect.Y;
                    rect.Y += changeY;
                }

                Canvas.SetTop(this, rect.Y);
                Height = rect.Height;
            }

            Model.Zones[ZoneIndex] = rect;
        }

        private static int zIndex = 0;
        private const int MinZoneWidth = 64;
        private const int MinZoneHeight = 72;

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            Canvas.SetZIndex(this, zIndex++);
            base.OnPreviewMouseDown(e);
        }

        private void UniversalDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (snappyX != null)
            {
                snappyX.Move((int)e.HorizontalChange);
            }

            if (snappyY != null)
            {
                snappyY.Move((int)e.VerticalChange);
            }

            UpdateFromSnappyHelpers();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            ((Panel)Parent).Children.Remove(this);
            Model.RemoveZoneAt(ZoneIndex);
        }

        // Corner dragging
        private void Caption_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.BothEdges, (int)_settings.WorkArea.Width);
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.BothEdges, (int)_settings.WorkArea.Height);
        }

        public CanvasLayoutModel Model { get => model; set => model = value; }

        public int ZoneIndex { get => zoneIndex; set => zoneIndex = value; }

        private void NWResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.BottomEdge, (int)_settings.WorkArea.Width);
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.BottomEdge, (int)_settings.WorkArea.Height);
        }

        private void NEResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.TopEdge, (int)_settings.WorkArea.Width);
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.BottomEdge, (int)_settings.WorkArea.Height);
        }

        private void SWResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.BottomEdge, (int)_settings.WorkArea.Width);
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.TopEdge, (int)_settings.WorkArea.Height);
        }

        private void SEResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.TopEdge, (int)_settings.WorkArea.Width);
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.TopEdge, (int)_settings.WorkArea.Height);
        }

        // Edge dragging
        private void NResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = null;
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.BottomEdge, (int)_settings.WorkArea.Height);
        }

        private void SResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = null;
            snappyY = NewDefaultSnappyHelper(false, ResizeMode.TopEdge, (int)_settings.WorkArea.Height);
        }

        private void WResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.BottomEdge, (int)_settings.WorkArea.Width);
            snappyY = null;
        }

        private void EResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, ResizeMode.TopEdge, (int)_settings.WorkArea.Width);
            snappyY = null;
        }
    }
}

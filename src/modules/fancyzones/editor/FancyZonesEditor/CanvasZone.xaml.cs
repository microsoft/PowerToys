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

        private int zoneIndex;

        private abstract class SnappyHelperBase
        {
            public int ScreenW { get; private set; }

            protected List<int> Snaps { get; private set; }

            protected int MinValue { get; private set; }

            protected int MaxValue { get; private set; }

            public int Position { get; protected set; }

            public int Mode { get; private set; }

            public const int ModeLow = 1;
            public const int ModeHigh = 2;
            public const int ModeBoth = 3;

            /// <summary>
            /// Initializes a new instance of the <see cref="SnappyHelperBase"/> class.
            ///     Just pass it the canvas arguments. Use mode
            ///     to tell it which edges of the existing masks to use when building its list
            ///     of snap points, and generally which edges to track. There will be two
            ///     SnappyHelpers, one for X-coordinates and one for
            ///     Y-coordinates, they work independently but share the same logic.
            /// </summary>
            /// <param name="zones">The list of rectangles describing all zones</param>
            /// <param name="zoneIndex">The index of the zone to track</param>
            /// <param name="isX"> Whether this is the X or Y SnappyHelper</param>
            /// <param name="mode"> One of the three modes of operation (for example: tracking left/right/both edges)</param>
            /// <param name="screen_w"> The size of the screen in this (X or Y) dimension</param>
            public SnappyHelperBase(IList<Int32Rect> zones, int zoneIndex, bool isX, int mode, int screen_w)
            {
                int track_p = isX ? zones[zoneIndex].X : zones[zoneIndex].Y;
                int track_w = isX ? zones[zoneIndex].Width : zones[zoneIndex].Height;
                int min_w = isX ? MinZoneWidth : MinZoneHeight;
                List<int> key_positions = new List<int>();
                for (int i = 0; i < zones.Count; ++i)
                {
                    if (i != zoneIndex)
                    {
                        int curr_p = isX ? zones[i].X : zones[i].Y;
                        int curr_w = isX ? zones[i].Width : zones[i].Height;
                        key_positions.Add(curr_p);
                        key_positions.Add(curr_p + curr_w);
                        if (mode == ModeBoth)
                        {
                            key_positions.Add(curr_p - track_w);
                            key_positions.Add(curr_p + curr_w - track_w);
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

                // Initialize minValue
                if (mode == ModeLow)
                {
                    // We're dragging the low edge, don't go below zero
                    MinValue = 0;
                }
                else if (mode == ModeHigh)
                {
                    // We're dragging the high edge, don't make the zone smaller than min_w
                    MinValue = track_p + min_w;
                }
                else if (mode == ModeBoth)
                {
                    // We're moving the window, don't move it below zero
                    MinValue = 0;
                }

                // Initialize maxValue
                if (mode == ModeLow)
                {
                    // We're dragging the low edge, it can't make the zone smaller than min_w
                    MaxValue = track_p + track_w - min_w;
                }
                else if (mode == ModeHigh)
                {
                    // We're dragging the high edge, don't go off the screen
                    MaxValue = screen_w;
                }
                else if (mode == ModeBoth)
                {
                    // We're moving the window, don't go off the screen (this time the lower edge is tracked)
                    MaxValue = screen_w - track_w;
                }

                // Initialize position
                if (mode == ModeLow)
                {
                    Position = track_p;
                }
                else if (mode == ModeHigh)
                {
                    Position = track_p + track_w;
                }
                else if (mode == ModeBoth)
                {
                    Position = track_p;
                }

                Mode = mode;
                this.ScreenW = screen_w;
            }

            public abstract void Move(int delta);
        }

        private class SnappyHelperSliding : SnappyHelperBase
        {
            private int MaxEnergy
            {
                get
                {
                    return (int)(0.008 * ScreenW);
                }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="SnappyHelperSliding"/> class.
            ///     Just pass it the canvas arguments. Use mode
            ///     to tell it which edges of the existing masks to use when building its list
            ///     of snap points, and generally which edges to track. There will be two
            ///     SnappyHelpers, one for X-coordinates and one for
            ///     Y-coordinates, they work independently but share the same logic.
            /// </summary>
            /// <param name="zones">The list of rectangles describing all zones</param>
            /// <param name="zoneIndex">The index of the zone to track</param>
            /// <param name="isX"> Whether this is the X or Y SnappyHelper</param>
            /// <param name="mode"> One of the three modes of operation (for example: tracking left/right/both edges)</param>
            /// <param name="screen_w"> The size of the screen in this (X or Y) dimension</param>
            public SnappyHelperSliding(IList<Int32Rect> zones, int zoneIndex, bool isX, int mode, int screen_w)
                : base(zones, zoneIndex, isX, mode, screen_w)
            {
            }

            public override void Move(int delta)
            {
                if (delta == 0)
                {
                    return;
                }

                int target_position = Position + delta;
                if (target_position > MaxValue)
                {
                    Position = MaxValue;
                    return;
                }

                if (target_position < MinValue)
                {
                    Position = MinValue;
                    return;
                }

                // are we flying over a snap position?
                int snapId = -1;

                if (delta > 0)
                {
                    for (int i = 0; i < Snaps.Count; ++i)
                    {
                        if (Position <= Snaps[i] && Snaps[i] <= target_position)
                        {
                            snapId = i;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = Snaps.Count - 1; i >= 0; --i)
                    {
                        if (target_position <= Snaps[i] && Snaps[i] <= Position)
                        {
                            snapId = i;
                            break;
                        }
                    }
                }

                if (snapId == -1)
                {
                    Position = target_position;
                }
                else
                {
                    int energy = target_position - Snaps[snapId];
                    Position = Snaps[snapId];
                    if (energy > MaxEnergy)
                    {
                        energy = 0;
                        Position += 1;
                    }
                    else if (energy < -MaxEnergy)
                    {
                        energy = 0;
                        Position -= 1;
                    }
                }
            }
        }

        private class SnappyHelperMagnetic : SnappyHelperBase
        {
            private List<int> magnetZoneSizes;
            private int freePosition;

            private int MagnetZoneMaxSize
            {
                get => (int)(0.08 * ScreenW);
            }

            public SnappyHelperMagnetic(IList<Int32Rect> zones, int zoneIndex, bool isX, int mode, int screen_w)
                : base(zones, zoneIndex, isX, mode, screen_w)
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

        // change to SnappyHelperSliding for different behavior
        private SnappyHelperBase NewDefaultSnappyHelper(bool isX, int mode, int screen_w)
        {
            return new SnappyHelperMagnetic(Model.Zones, ZoneIndex, isX, mode, screen_w);
        }

        private void UpdateFromSnappyHelpers()
        {
            Int32Rect rect = Model.Zones[ZoneIndex];

            if (snappyX != null)
            {
                if (snappyX.Mode == SnappyHelperBase.ModeLow)
                {
                    int changeX = snappyX.Position - rect.X;
                    rect.X += changeX;
                    rect.Width -= changeX;
                }
                else if (snappyX.Mode == SnappyHelperBase.ModeHigh)
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
                if (snappyY.Mode == SnappyHelperBase.ModeLow)
                {
                    int changeY = snappyY.Position - rect.Y;
                    rect.Y += changeY;
                    rect.Height -= changeY;
                }
                else if (snappyY.Mode == SnappyHelperBase.ModeHigh)
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
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeBoth, Model.ReferenceWidth);
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeBoth, Model.ReferenceHeight);
        }

        private Settings _settings = ((App)Application.Current).ZoneSettings;

        public CanvasLayoutModel Model { get => model; set => model = value; }

        public int ZoneIndex { get => zoneIndex; set => zoneIndex = value; }

        private void NWResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeLow, Model.ReferenceWidth);
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeLow, Model.ReferenceHeight);
        }

        private void NEResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeHigh, Model.ReferenceWidth);
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeLow, Model.ReferenceHeight);
        }

        private void SWResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeLow, Model.ReferenceWidth);
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeHigh, Model.ReferenceHeight);
        }

        private void SEResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeHigh, Model.ReferenceWidth);
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeHigh, Model.ReferenceHeight);
        }

        // Edge dragging
        private void NResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = null;
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeLow, Model.ReferenceHeight);
        }

        private void SResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = null;
            snappyY = NewDefaultSnappyHelper(false, SnappyHelperBase.ModeHigh, Model.ReferenceHeight);
        }

        private void WResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeLow, Model.ReferenceWidth);
            snappyY = null;
        }

        private void EResize_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            snappyX = NewDefaultSnappyHelper(true, SnappyHelperBase.ModeHigh, Model.ReferenceWidth);
            snappyY = null;
        }
    }
}

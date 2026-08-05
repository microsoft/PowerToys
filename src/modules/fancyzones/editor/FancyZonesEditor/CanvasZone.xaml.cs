// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

using FancyZonesEditor.Helpers;
using FancyZonesEditor.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;

namespace FancyZonesEditor
{
    /// <summary>
    /// Once you've "Commit"ted the starter grid, then the Zones within the grid come to life for you to be able to further subdivide them
    /// using splitters
    /// </summary>
    public sealed partial class CanvasZone : UserControl
    {
        private const int MinZoneWidth = 64;
        private const int MinZoneHeight = 72;

        private static int zIndex;

        private readonly int defaultMoveAmount = 10;
        private readonly int smallMoveAmount = 1;

        private CanvasLayoutModel model;
        private int zoneIndex;
        private SnappyHelperBase snappyX;
        private SnappyHelperBase snappyY;

        public CanvasZone()
        {
            InitializeComponent();
            Canvas.SetZIndex(this, zIndex++);
            SizeChanged += CanvasZone_SizeChanged;
            GotFocus += CanvasZone_GotFocus;
            LostFocus += CanvasZone_LostFocus;

            // The drag thumbs cover the whole zone and mark the pointer event handled, so the
            // focus/z-order shim must run even for already-handled events. WPF used the
            // tunneling PreviewMouseDown, which WinUI does not have.
            AddHandler(PointerPressedEvent, new PointerEventHandler(Zone_PointerPressed), handledEventsToo: true);
        }

        public enum ResizeMode
        {
            BottomEdge,
            TopEdge,
            BothEdges,
        }

        public CanvasLayoutModel Model { get => model; set => model = value; }

        public int ZoneIndex { get => zoneIndex; set => zoneIndex = value; }

        public void FocusZone()
        {
            Focus(FocusState.Programmatic);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new CanvasZoneAutomationPeer(this);
        }

        private sealed partial class CanvasZoneAutomationPeer : FrameworkElementAutomationPeer
        {
            public CanvasZoneAutomationPeer(CanvasZone owner)
                : base(owner)
            {
            }

            protected override string GetClassNameCore() => nameof(CanvasZone);

            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        }

        private static bool IsKeyDown(VirtualKey key)
        {
            return InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
        }

        private static bool IsShiftKeyDown() => IsKeyDown(VirtualKey.Shift);

        private static bool IsCtrlKeyDown() => IsKeyDown(VirtualKey.Control);

        private void CanvasZone_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // using current culture as this is end user facing
            WidthLabel.Text = Width.ToString(CultureInfo.CurrentCulture);
            HeightLabel.Text = Height.ToString(CultureInfo.CurrentCulture);
            AutomationProperties.SetName(
                this,
#pragma warning disable SA1118 // Parameter should not span multiple lines
                ResourceLoaderInstance.GetString("Zone_Name") + " " + (ZoneIndex + 1).ToString(CultureInfo.CurrentCulture) + ". " +
                ResourceLoaderInstance.GetString("Width_Name") + ": " + WidthLabel.Text + ", " +
                ResourceLoaderInstance.GetString("Height_Name") + ": " + HeightLabel.Text);
#pragma warning restore SA1118 // Parameter should not span multiple lines
        }

        // WinUI has no Style.Triggers, so the focused chrome is applied directly.
        private void CanvasZone_GotFocus(object sender, RoutedEventArgs e)
        {
            RootBorder.BorderThickness = new Thickness(4);
            RootBorder.BorderBrush = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush;
        }

        private void CanvasZone_LostFocus(object sender, RoutedEventArgs e)
        {
            RootBorder.BorderThickness = new Thickness(1);
            RootBorder.BorderBrush = Application.Current.Resources["CanvasZoneBorderBrush"] as Brush;
        }

        private void Zone_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Raise the zone above its siblings and give it (keyboard) focus when clicked.
            Canvas.SetZIndex(this, zIndex++);
            Focus(FocusState.Programmatic);
        }

        private SnappyHelperMagnetic NewMagneticSnapper(bool isX, ResizeMode mode)
        {
            Rect workingArea = App.Overlay.WorkArea;
            int screenAxisOrigin = (int)(isX ? workingArea.Left : workingArea.Top);
            int screenAxisSize = (int)(isX ? workingArea.Width : workingArea.Height);
            return new SnappyHelperMagnetic(Model.Zones, ZoneIndex, isX, mode, screenAxisOrigin, screenAxisSize);
        }

        private SnappyHelperNonMagnetic NewNonMagneticSnapper(bool isX, ResizeMode mode)
        {
            Rect workingArea = App.Overlay.WorkArea;
            int screenAxisOrigin = (int)(isX ? workingArea.Left : workingArea.Top);
            int screenAxisSize = (int)(isX ? workingArea.Width : workingArea.Height);
            return new SnappyHelperNonMagnetic(Model.Zones, ZoneIndex, isX, mode, screenAxisOrigin, screenAxisSize);
        }

        private void UpdateFromSnappyHelpers()
        {
            if (ZoneIndex >= Model.Zones.Count)
            {
                return;
            }

            RectInt32 rect = Model.Zones[ZoneIndex];

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

        private void UniversalDragDelta(object sender, DragDeltaEventArgs e)
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

        // Corner dragging
        private void Caption_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.BothEdges);
            snappyY = NewMagneticSnapper(false, ResizeMode.BothEdges);
        }

        private void NWResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.BottomEdge);
            snappyY = NewMagneticSnapper(false, ResizeMode.BottomEdge);
        }

        private void NEResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.TopEdge);
            snappyY = NewMagneticSnapper(false, ResizeMode.BottomEdge);
        }

        private void SWResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.BottomEdge);
            snappyY = NewMagneticSnapper(false, ResizeMode.TopEdge);
        }

        private void SEResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.TopEdge);
            snappyY = NewMagneticSnapper(false, ResizeMode.TopEdge);
        }

        // Edge dragging
        private void NResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = null;
            snappyY = NewMagneticSnapper(false, ResizeMode.BottomEdge);
        }

        private void SResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = null;
            snappyY = NewMagneticSnapper(false, ResizeMode.TopEdge);
        }

        private void WResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.BottomEdge);
            snappyY = null;
        }

        private void EResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            snappyX = NewMagneticSnapper(true, ResizeMode.TopEdge);
            snappyY = null;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            RemoveZone();
        }

        private void RemoveZone()
        {
            ((Panel)Parent).Children.Remove(this);
            Model.RemoveZoneAt(ZoneIndex);
        }

        private void Zone_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                return;
            }

            e.Handled = true;

            if (e.Key == VirtualKey.Delete)
            {
                RemoveZone();
                return;
            }

            var moveValue = IsCtrlKeyDown() ? smallMoveAmount : defaultMoveAmount;
            if (IsShiftKeyDown())
            {
                moveValue = Math.Max(1, moveValue / 2);
            }

            if (e.Key == VirtualKey.Right)
            {
                if (IsShiftKeyDown())
                {
                    // Make the zone larger (width)
                    MoveZoneX(moveValue, ResizeMode.TopEdge, ResizeMode.BottomEdge);
                    MoveZoneX(-moveValue, ResizeMode.BottomEdge, ResizeMode.BottomEdge);
                }
                else
                {
                    // Move zone right
                    MoveZoneX(moveValue, ResizeMode.BothEdges, ResizeMode.BothEdges);
                }
            }
            else if (e.Key == VirtualKey.Left)
            {
                if (IsShiftKeyDown())
                {
                    // Make the zone smaller (width)
                    MoveZoneX(-moveValue, ResizeMode.TopEdge, ResizeMode.BottomEdge);
                    MoveZoneX(moveValue, ResizeMode.BottomEdge, ResizeMode.BottomEdge);
                }
                else
                {
                    // Move zone left
                    MoveZoneX(-moveValue, ResizeMode.BothEdges, ResizeMode.BothEdges);
                }
            }
            else if (e.Key == VirtualKey.Up)
            {
                if (IsShiftKeyDown())
                {
                    // Make the zone larger (height)
                    MoveZoneY(moveValue, ResizeMode.TopEdge, ResizeMode.BottomEdge);
                    MoveZoneY(-moveValue, ResizeMode.BottomEdge, ResizeMode.BottomEdge);
                }
                else
                {
                    // Move zone up
                    MoveZoneY(-moveValue, ResizeMode.BothEdges, ResizeMode.BothEdges);
                }
            }
            else if (e.Key == VirtualKey.Down)
            {
                if (IsShiftKeyDown())
                {
                    // Make the zone smaller (height)
                    MoveZoneY(-moveValue, ResizeMode.TopEdge, ResizeMode.BottomEdge);
                    MoveZoneY(moveValue, ResizeMode.BottomEdge, ResizeMode.BottomEdge);
                }
                else
                {
                    // Move zone down
                    MoveZoneY(moveValue, ResizeMode.BothEdges, ResizeMode.BothEdges);
                }
            }
        }

        private void MoveZoneX(int value, ResizeMode top, ResizeMode bottom)
        {
            snappyX = NewNonMagneticSnapper(true, top);
            snappyY = NewNonMagneticSnapper(false, bottom);
            snappyX.Move(value);
            UpdateFromSnappyHelpers();
        }

        private void MoveZoneY(int value, ResizeMode top, ResizeMode bottom)
        {
            snappyX = NewNonMagneticSnapper(true, bottom);
            snappyY = NewNonMagneticSnapper(false, top);
            snappyY.Move(value);
            UpdateFromSnappyHelpers();
        }

        private abstract class SnappyHelperBase
        {
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
            /// <param name="screenAxisOrigin"> The origin (left/top) of the screen in this (X or Y) dimension</param>
            /// <param name="screenAxisSize"> The size of the screen in this (X or Y) dimension</param>
            public SnappyHelperBase(IList<RectInt32> zones, int zoneIndex, bool isX, ResizeMode mode, int screenAxisOrigin, int screenAxisSize)
            {
                int zonePosition = isX ? zones[zoneIndex].X : zones[zoneIndex].Y;
                int zoneAxisSize = isX ? zones[zoneIndex].Width : zones[zoneIndex].Height;
                int minAxisSize = isX ? MinZoneWidth : MinZoneHeight;
                List<int> keyPositions = new List<int>();
                for (int i = 0; i < zones.Count; ++i)
                {
                    if (i != zoneIndex)
                    {
                        int ithZonePosition = isX ? zones[i].X : zones[i].Y;
                        int ithZoneAxisSize = isX ? zones[i].Width : zones[i].Height;
                        keyPositions.Add(ithZonePosition);
                        keyPositions.Add(ithZonePosition + ithZoneAxisSize);
                        if (mode == ResizeMode.BothEdges)
                        {
                            keyPositions.Add(ithZonePosition - zoneAxisSize);
                            keyPositions.Add(ithZonePosition + ithZoneAxisSize - zoneAxisSize);
                        }
                    }
                }

                foreach (Rect singleMonitor in App.Overlay.WorkAreas)
                {
                    int monitorPositionLow = (int)(isX ? singleMonitor.Left : singleMonitor.Top);
                    int monitorPositionHigh = (int)(isX ? singleMonitor.Right : singleMonitor.Bottom);
                    keyPositions.Add(monitorPositionLow - screenAxisOrigin);
                    keyPositions.Add(monitorPositionHigh - screenAxisOrigin);
                    if (mode == ResizeMode.BothEdges)
                    {
                        keyPositions.Add(monitorPositionLow - screenAxisOrigin - zoneAxisSize);
                        keyPositions.Add(monitorPositionHigh - screenAxisOrigin - zoneAxisSize);
                    }
                }

                // Remove duplicates and sort
                keyPositions.Sort();
                Snaps = new List<int>();
                if (keyPositions.Count > 0)
                {
                    Snaps.Add(keyPositions[0]);
                    for (int i = 1; i < keyPositions.Count; ++i)
                    {
                        if (keyPositions[i] != keyPositions[i - 1])
                        {
                            Snaps.Add(keyPositions[i]);
                        }
                    }
                }

                switch (mode)
                {
                    case ResizeMode.BottomEdge:
                        // We're dragging the low edge, don't go below zero
                        MinValue = 0;

                        // It can't make the zone smaller than minAxisSize
                        MaxValue = zonePosition + zoneAxisSize - minAxisSize;
                        Position = zonePosition;
                        break;
                    case ResizeMode.TopEdge:
                        // We're dragging the high edge, don't make the zone smaller than minAxisSize
                        MinValue = zonePosition + minAxisSize;

                        // Don't go off the screen
                        MaxValue = screenAxisSize;
                        Position = zonePosition + zoneAxisSize;
                        break;
                    case ResizeMode.BothEdges:
                        // We're moving the window, don't move it below zero
                        MinValue = 0;

                        // Don't go off the screen (this time the lower edge is tracked)
                        MaxValue = screenAxisSize - zoneAxisSize;
                        Position = zonePosition;
                        break;
                }

                Mode = mode;
                this.ScreenW = screenAxisSize;
            }

            public int ScreenW { get; private set; }

            public int Position { get; protected set; }

            public ResizeMode Mode { get; private set; }

            protected List<int> Snaps { get; private set; }

            protected int MinValue { get; private set; }

            protected int MaxValue { get; private set; }

            public abstract void Move(int delta);
        }

        private sealed class SnappyHelperMagnetic : SnappyHelperBase
        {
            private List<int> magnetZoneSizes;
            private int freePosition;

            public SnappyHelperMagnetic(IList<RectInt32> zones, int zoneIndex, bool isX, ResizeMode mode, int screenAxisOrigin, int screenAxisSize)
                : base(zones, zoneIndex, isX, mode, screenAxisOrigin, screenAxisSize)
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

            private int MagnetZoneMaxSize
            {
                get => (int)(0.08 * ScreenW);
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

        private sealed class SnappyHelperNonMagnetic : SnappyHelperBase
        {
            public SnappyHelperNonMagnetic(IList<RectInt32> zones, int zoneIndex, bool isX, ResizeMode mode, int screenAxisOrigin, int screenAxisSize)
                : base(zones, zoneIndex, isX, mode, screenAxisOrigin, screenAxisSize)
            {
            }

            public override void Move(int delta)
            {
                var pos = Position + delta;
                Position = Math.Max(Math.Min(MaxValue, pos), MinValue);
            }
        }
    }
}

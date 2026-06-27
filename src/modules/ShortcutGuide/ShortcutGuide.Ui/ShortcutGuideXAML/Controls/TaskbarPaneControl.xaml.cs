// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Helpers;
using Windows.Foundation;
using static ShortcutGuide.NativeMethods;

namespace ShortcutGuide.Controls
{
    /// <summary>
    /// The taskbar number-indicator pseudo-window that used to be
    /// <c>TaskbarWindow</c>. Now a regular <see cref="UserControl"/> hosted
    /// inside <see cref="OverlayWindow"/>; the overlay applies the values
    /// returned from <see cref="UpdateTasklistButtons"/> to position the
    /// control inside its <see cref="Canvas"/>.
    /// </summary>
    public sealed partial class TaskbarPaneControl : UserControl
    {
        /// <summary>
        /// The window-relative layout the overlay should apply to this control.
        /// All values are in DIPs.
        /// </summary>
        public readonly record struct TaskbarPaneLayout(double Left, double Top, double Width, double Height);

        public TaskbarPaneControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Rebuilds the indicator children from the current taskbar buttons
        /// and returns the desired position/size the overlay should apply.
        /// Returns <see langword="null"/> when there are no taskbar buttons
        /// (caller should hide the control). The layout adapts to the screen
        /// edge the taskbar is docked to: indicators run horizontally for a
        /// top/bottom taskbar and vertically for a left/right taskbar, with the
        /// tail pointing toward the taskbar.
        /// </summary>
        /// <param name="overlayPhysicalOriginX">The overlay window's physical left in screen coordinates.</param>
        /// <param name="overlayPhysicalOriginY">The overlay window's physical top in screen coordinates.</param>
        /// <param name="dpi">DPI scale factor of the host overlay window.</param>
        /// <param name="workAreaPhysical">The work area of the overlay's monitor in physical pixels.</param>
        /// <param name="edge">The screen edge the taskbar is docked to.</param>
        internal TaskbarPaneLayout? UpdateTasklistButtons(int overlayPhysicalOriginX, int overlayPhysicalOriginY, float dpi, Rect workAreaPhysical, TaskbarEdge edge)
        {
            TasklistButton[] buttons = [];
            try
            {
                buttons = TasklistPositions.GetButtons();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to enumerate taskbar buttons via TasklistPositions.GetButtons.", ex);
            }

            if (buttons.Length == 0)
            {
                this.KeyHolder.Children.Clear();
                return null;
            }

            // Each indicator is a standalone tooltip: a 40x40 body plus a 6px
            // triangle tail on the side that faces the taskbar. The "thickness"
            // (minor axis: body + tail) sits perpendicular to the taskbar edge;
            // the body alone (40px) runs along the taskbar.
            const double IndicatorBodyDip = 40;
            const double TriangleTailDip = 6;
            const double EdgeMarginDip = 8;
            double indicatorThicknessDip = IndicatorBodyDip + TriangleTailDip;

            this.KeyHolder.Children.Clear();

            bool horizontal = edge is TaskbarEdge.Top or TaskbarEdge.Bottom;
            IndicatorTailDirection tail = edge switch
            {
                TaskbarEdge.Bottom => IndicatorTailDirection.Down,
                TaskbarEdge.Top => IndicatorTailDirection.Up,
                TaskbarEdge.Left => IndicatorTailDirection.Left,
                _ => IndicatorTailDirection.Right,
            };

            if (horizontal)
            {
                double leftmostPhysicalX = buttons[0].X;
                double rightmostPhysicalX = buttons[0].X + buttons[0].Width;
                foreach (TasklistButton b in buttons)
                {
                    leftmostPhysicalX = Math.Min(leftmostPhysicalX, b.X);
                    rightmostPhysicalX = Math.Max(rightmostPhysicalX, b.X + b.Width);
                }

                foreach (TasklistButton b in buttons)
                {
                    TaskbarIndicator indicator = CreateIndicator(b, tail);
                    this.KeyHolder.Children.Add(indicator);

                    // Center each indicator's body over its button along the X axis.
                    double buttonCenterPhysical = b.X + (b.Width / 2.0);
                    double indicatorLeftDip = ((buttonCenterPhysical - leftmostPhysicalX) / dpi) - (IndicatorBodyDip / 2.0);
                    Canvas.SetLeft(indicator, indicatorLeftDip);
                    Canvas.SetTop(indicator, 0);
                }

                // Anchor the strip just inside the taskbar edge: the tail tip
                // sits EdgeMarginDip away from the bottom (or top) of the work area.
                double paneOriginPhysicalY = edge == TaskbarEdge.Bottom
                    ? workAreaPhysical.Bottom - ((indicatorThicknessDip + EdgeMarginDip) * dpi)
                    : workAreaPhysical.Top + (EdgeMarginDip * dpi);

                double paneLeftDip = (leftmostPhysicalX - overlayPhysicalOriginX) / dpi;
                double paneTopDip = (paneOriginPhysicalY - overlayPhysicalOriginY) / dpi;
                double paneWidthDip = (rightmostPhysicalX - leftmostPhysicalX) / dpi;

                return new TaskbarPaneLayout(paneLeftDip, paneTopDip, paneWidthDip, indicatorThicknessDip);
            }
            else
            {
                double topmostPhysicalY = buttons[0].Y;
                double bottommostPhysicalY = buttons[0].Y + buttons[0].Height;
                foreach (TasklistButton b in buttons)
                {
                    topmostPhysicalY = Math.Min(topmostPhysicalY, b.Y);
                    bottommostPhysicalY = Math.Max(bottommostPhysicalY, b.Y + b.Height);
                }

                foreach (TasklistButton b in buttons)
                {
                    TaskbarIndicator indicator = CreateIndicator(b, tail);
                    this.KeyHolder.Children.Add(indicator);

                    // Center each indicator's body over its button along the Y axis.
                    double buttonCenterPhysical = b.Y + (b.Height / 2.0);
                    double indicatorTopDip = ((buttonCenterPhysical - topmostPhysicalY) / dpi) - (IndicatorBodyDip / 2.0);
                    Canvas.SetTop(indicator, indicatorTopDip);
                    Canvas.SetLeft(indicator, 0);
                }

                // Anchor the strip just inside the taskbar edge: the tail tip
                // sits EdgeMarginDip away from the left (or right) of the work area.
                double paneOriginPhysicalX = edge == TaskbarEdge.Left
                    ? workAreaPhysical.Left + (EdgeMarginDip * dpi)
                    : workAreaPhysical.Right - ((indicatorThicknessDip + EdgeMarginDip) * dpi);

                double paneLeftDip = (paneOriginPhysicalX - overlayPhysicalOriginX) / dpi;
                double paneTopDip = (topmostPhysicalY - overlayPhysicalOriginY) / dpi;
                double paneHeightDip = (bottommostPhysicalY - topmostPhysicalY) / dpi;

                return new TaskbarPaneLayout(paneLeftDip, paneTopDip, indicatorThicknessDip, paneHeightDip);
            }
        }

        private static TaskbarIndicator CreateIndicator(TasklistButton button, IndicatorTailDirection tail)
        {
            TaskbarIndicator indicator = new()
            {
                Label = button.Keynum >= 10 ? "0" : button.Keynum.ToString(CultureInfo.InvariantCulture),
            };
            indicator.ApplyTailDirection(tail);
            return indicator;
        }
    }
}

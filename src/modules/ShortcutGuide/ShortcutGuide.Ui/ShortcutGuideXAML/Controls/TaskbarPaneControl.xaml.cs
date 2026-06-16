// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Helpers;
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
        /// (caller should hide the control).
        /// </summary>
        /// <param name="overlayPhysicalOriginX">The overlay window's physical left in screen coordinates.</param>
        /// <param name="overlayPhysicalOriginY">The overlay window's physical top in screen coordinates.</param>
        /// <param name="dpi">DPI scale factor of the host overlay window.</param>
        /// <param name="workAreaBottomPhysical">Bottom of the work area in physical pixels.</param>
        public TaskbarPaneLayout? UpdateTasklistButtons(int overlayPhysicalOriginX, int overlayPhysicalOriginY, float dpi, double workAreaBottomPhysical)
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

            // Each indicator is a standalone tooltip (~36px body + ~6px triangle
            // tail). Position the pane so that the bottom of the triangle sits
            // 8px above the bottom of the work area (taskbar edge).
            const double IndicatorBodyDip = 40;
            const double TriangleTailDip = 6;
            const double BottomMarginDip = 8;
            double indicatorTotalHeightDip = IndicatorBodyDip + TriangleTailDip;

            double paneOriginPhysicalY = workAreaBottomPhysical - ((indicatorTotalHeightDip + BottomMarginDip) * dpi);

            this.KeyHolder.Children.Clear();

            double leftmostPhysicalX = buttons[0].X;
            double rightmostPhysicalX = buttons[0].X + buttons[0].Width;

            foreach (TasklistButton b in buttons)
            {
                TaskbarIndicator indicator = new()
                {
                    Label = b.Keynum >= 10 ? "0" : b.Keynum.ToString(CultureInfo.InvariantCulture),
                };

                this.KeyHolder.Children.Add(indicator);

                // Center each indicator over its button
                double buttonCenterPhysical = b.X + (b.Width / 2.0);
                double indicatorLeftDip = ((buttonCenterPhysical - leftmostPhysicalX) / dpi) - (IndicatorBodyDip / 2.0);
                Canvas.SetLeft(indicator, indicatorLeftDip);
                Canvas.SetTop(indicator, 0);

                rightmostPhysicalX = Math.Max(rightmostPhysicalX, b.X + b.Width);
            }

            double paneLeftDip = (leftmostPhysicalX - overlayPhysicalOriginX) / dpi;
            double paneTopDip = (paneOriginPhysicalY - overlayPhysicalOriginY) / dpi;
            double paneWidthDip = (rightmostPhysicalX - leftmostPhysicalX) / dpi;

            return new TaskbarPaneLayout(paneLeftDip, paneTopDip, paneWidthDip, indicatorTotalHeightDip);
        }
    }
}

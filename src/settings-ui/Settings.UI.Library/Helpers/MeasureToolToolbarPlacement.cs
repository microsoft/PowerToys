// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    /// <summary>
    /// Pure geometry for anchoring the Screen Ruler toolbar within a monitor's work area. Contains
    /// no WinUI/WinRT types so it can be exercised by plain unit tests; callers convert the
    /// returned physical-pixel coordinates directly into an <c>AppWindow.MoveAndResize</c> call.
    /// </summary>
    public static class MeasureToolToolbarPlacement
    {
        /// <summary>
        /// Converts the persisted integer setting to a supported anchor, falling back to the
        /// documented default when settings.json contains an unknown value.
        /// </summary>
        public static MeasureToolToolbarPosition Normalize(int value)
        {
            return Enum.IsDefined(typeof(MeasureToolToolbarPosition), value)
                ? (MeasureToolToolbarPosition)value
                : MeasureToolToolbarPosition.TopCenter;
        }

        /// <summary>
        /// Resolves the top-left corner (in physical pixels, absolute screen coordinates) of the
        /// toolbar's visible surface for <paramref name="position"/> within <paramref name="workAreaX"/>/
        /// Y/Width/Height (the target monitor's work area, also in physical pixels - e.g.
        /// <c>DisplayArea.WorkArea</c>). <paramref name="toolbarWidthDip"/>/<paramref name="toolbarHeightDip"/>
        /// are the visible surface's DIP size; <paramref name="insetDip"/> is the gap kept between
        /// the surface and the work-area edge (per spec: 24 DIP).
        /// </summary>
        /// <remarks>
        /// The result is clamped so the toolbar's surface never extends past the work area: if the
        /// toolbar (plus inset) would overflow an edge - e.g. a narrow secondary monitor, or a very
        /// large DPI scale - the anchor falls back to flush against that edge instead of clipping
        /// off-screen. This mirrors <c>FlyoutWindowHelper</c>'s clamping for the other flyout-style
        /// PowerToys overlays.
        /// </remarks>
        public static (int X, int Y) GetAnchorPosition(
            MeasureToolToolbarPosition position,
            int workAreaX,
            int workAreaY,
            int workAreaWidth,
            int workAreaHeight,
            double toolbarWidthDip,
            double toolbarHeightDip,
            double insetDip,
            double dpiScale)
        {
            int widthPx = (int)Math.Ceiling(toolbarWidthDip * dpiScale);
            int heightPx = (int)Math.Ceiling(toolbarHeightDip * dpiScale);
            int insetPx = (int)Math.Ceiling(insetDip * dpiScale);

            int x = GetColumn(position) switch
            {
                -1 => workAreaX + insetPx,
                0 => workAreaX + ((workAreaWidth - widthPx) / 2),
                _ => workAreaX + workAreaWidth - widthPx - insetPx,
            };

            int y = GetRow(position) switch
            {
                -1 => workAreaY + insetPx,
                0 => workAreaY + ((workAreaHeight - heightPx) / 2),
                _ => workAreaY + workAreaHeight - heightPx - insetPx,
            };

            // Work-area clamping: never let the surface extend past either edge, even if the
            // formula above (e.g. a centered anchor on a monitor narrower than the toolbar) would
            // place it there. Clamping to the *lower* bound first and the upper bound second means
            // a work area smaller than the toolbar still anchors flush to the leading edge rather
            // than the trailing one.
            x = Clamp(x, workAreaX, workAreaX + workAreaWidth - widthPx);
            y = Clamp(y, workAreaY, workAreaY + workAreaHeight - heightPx);

            return (x, y);
        }

        // -1 = left column, 0 = center column, 1 = right column.
        private static int GetColumn(MeasureToolToolbarPosition position) => position switch
        {
            MeasureToolToolbarPosition.TopLeft or MeasureToolToolbarPosition.BottomLeft => -1,
            MeasureToolToolbarPosition.TopCenter or MeasureToolToolbarPosition.BottomCenter => 0,
            _ => 1,
        };

        // -1 = top row, 1 = bottom row.
        private static int GetRow(MeasureToolToolbarPosition position) => position switch
        {
            MeasureToolToolbarPosition.TopLeft or MeasureToolToolbarPosition.TopCenter or MeasureToolToolbarPosition.TopRight => -1,
            _ => 1,
        };

        private static int Clamp(int value, int min, int max)
        {
            // When the toolbar is larger than the work area, max < min; prefer the leading edge
            // (min) rather than letting the toolbar spill past the trailing edge.
            if (max < min)
            {
                return min;
            }

            return Math.Clamp(value, min, max);
        }
    }
}

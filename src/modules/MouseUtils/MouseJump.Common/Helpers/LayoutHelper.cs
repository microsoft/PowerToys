// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;

using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;
using MouseJump.Models.Styles;

namespace MouseJump.Common.Helpers;

public static class LayoutHelper
{
    /*

        we use a poor-man's version of the w3c "box model" (see FancyMouse.Models.Styles.BoxStyle)
        to calculate sizes and positions of individual ui components. the diagram below shows the
        logical organisation of components:

        * the overall PreviewWindow - a container for all of the ui components. this directly
          hosts the outer preview border control, a child PreviewPane and a status bar control

          +--------[preview window]--------+
          |▒▒▒▒▒▒▒▒▒[outer border]▒▒▒▒▒▒▒▒▒|
          |▒▒+------[preview pane]------+▒▒|
          |▒▒|                          |▒▒|
          |▒▒|                          |▒▒|
          |▒▒|                          |▒▒|
          |▒▒+--------------------------+▒▒|
          |▒▒+-------[status bar]-------+▒▒|
          |▒▒|░░░░░░░░░░░░░░░░░░░░░░░░░░|▒▒|
          |▒▒+--------------------------+▒▒|
          |▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒|
          +--------------------------------+

        * the child PreviewPane - this contains the preview background, the padding from the
          outer border, the screen bezels and screenshot images. the "device grid" in the diagram
          is a logical grid - it doesn't exist in the layout or control hierarchy, and only
          exists as a set of temporary bounds during the actual layout calculation.

          +---------------------------[preview pane]----------------------------+
          |+---------------------------[background]----------------------------+|
          ||░░░░░░░░░░░░░░░░░░░░░░░░░░░░░[padding]░░░░░░░░░░░░░░░░░░░░░░░░░░░░░||
          ||▒▒+------------------------[device grid]-+----------------------+▒▒||
          ||▒▒|                                      |▓▓▓▓▓▓[device 2]▓▓▓▓▓▓|▒▒||
          ||▒▒|                                      |▓▓░░░░[screen 1]░░░░▓▓|▒▒||
          ||▒▒|▓▓▓▓▓▓▓▓▓▓▓▓▓▓[device 1]▓▓▓▓▓▓▓▓▓▓▓▓▓▓|▓▓░░              ░░▓▓|▒▒||
          ||▒▒|▓▓░░░░[screen 1]░░░░░░[screen 2]░░░░▓▓|▓▓░░              ░░▓▓|▒▒||
          ||▒▒|▓▓░░              ░░              ░░▓▓|▓▓░░              ░░▓▓|▒▒||
          ||▒▒|▓▓░░              ░░              ░░▓▓|▓▓░░░░░░░░░░░░░░░░░░▓▓|▒▒||
          ||▒▒|▓▓░░              ░░              ░░▓▓|▓▓░░░░[screen 2]░░░░▓▓|▒▒||
          ||▒▒|▓▓░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░▓▓|▓▓░░              ░░▓▓|▒▒||
          ||▒▒|▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓|▓▓░░              ░░▓▓|▒▒||
          ||▒▒|                                      |▓▓░░              ░░▓▓|▒▒||
          ||▒▒|                                      |▓▓░░░░░░░░░░░░░░░░░░▓▓|▒▒||
          ||▒▒|                                      |▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓|▒▒||
          ||▒▒+--------------------------------------+----------------------+▒▒||
          ||▒▒|        ▓▓▓▓▓▓[device 3]▓▓▓▓▓▓        |                      |▒▒||
          ||▒▒|        ▓▓░░░░[screen 1]░░░░▓▓        |                      |▒▒||
          ||▒▒|        ▓▓░░              ░░▓▓        |                      |▒▒||
          ||▒▒|        ▓▓░░              ░░▓▓        |   ▓▓▓[device 4]▓▓▓   |▒▒||
          ||▒▒|        ▓▓░░              ░░▓▓        |   ▓▓░[screen 1]░▓▓   |▒▒||
          ||▒▒|        ▓▓░░░░░░░░░░░░░░░░░░▓▓        |   ▓▓░░        ░░▓▓   |▒▒||
          ||▒▒|        ▓▓░░░░[screen 2]░░░░▓▓        |   ▓▓░░        ░░▓▓   |▒▒||
          ||▒▒|        ▓▓░░              ░░▓▓        |   ▓▓░░░░░░░░░░░░▓▓   |▒▒||
          ||▒▒|        ▓▓░░              ░░▓▓        |   ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓   |▒▒||
          ||▒▒|        ▓▓░░              ░░▓▓        |                      |▒▒||
          ||▒▒|        ▓▓░░░░░░░░░░░░░░░░░░▓▓        |                      |▒▒||
          ||▒▒|        ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓        |                      |▒▒||
          ||▒▒+-------------------------------------------------------------+▒▒||
          ||▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒||
          |+-------------------------------------------------------------------+|
          +---------------------------------------------------------------------+

        note that the style settings in the config file get chopped up into two
        different layouts - the outer border's *margin* and *thickness* belong
        in the PreviewWindow but the outer *padding* is converted into an inner
        padding inside the PreviewPane so the background can belong inside the
        PreviewPane and be the correct size, rather than owned by the outer
        PreviewWindow.

    */

    /// <summary>
    /// Returns the smallest rectangle that contains all of the specified screen bounds.
    /// </summary>
    public static RectangleInfo GetCombinedScreenBounds(List<RectangleInfo> screenBounds)
    {
        // we can't simply start with (0,0,0,0) and expand to encompass all screen bounds because
        // (0,0,0,0) might not actually exist in the combined region - for example if
        // "screenBounds = [(100, 100, 100, 100)]" we'd end up (incorrectly) with (0, 0, 200, 200).
        // our actual approach is:
        // * if the input is a single bounds we'll just return that
        // * otherwise we use the *first* bounds as the seed for combining the remaining bounds
        return screenBounds.Skip(1).Aggregate(
            seed: screenBounds.First(),
            (combined, screenBounds) => combined.Union(screenBounds));
    }

    /// <param name="maximumSize">
    /// An optional upper bound on the layout's size.
    /// If specified, device and screen dimensions will be scaled down as required in order for the final layout to fit inside the bounds.
    /// If <see langword="null"/>, the layout size will be calculated to allow full-size device and screen visualisations.
    /// </param>
    public static PreviewLayout GetPreviewLayout(
        PreviewStyle previewStyle, DisplayInfo displayInfo, SizeInfo? maximumSize = null)
    {
        ArgumentNullException.ThrowIfNull(previewStyle);
        ArgumentNullException.ThrowIfNull(displayInfo);

        var previewWindowStyle = LayoutHelper.GetPreviewWindowStyle(previewStyle.CanvasStyle);
        var previewPaneStyle = LayoutHelper.GetPreviewPaneStyle(previewStyle.CanvasStyle);

        // arrange the preview, canvas, devices and screens
        var previewLayout = LayoutHelper.CreateInitialPreviewLayout(previewStyle, previewWindowStyle, previewPaneStyle, maximumSize);
        LayoutHelper.AddDeviceAndScreenStyles(previewLayout, previewStyle, displayInfo);
        LayoutHelper.ArrangeAndScaleDeviceLayouts(previewLayout);
        LayoutHelper.ArrangeAndScaleScreenLayouts(previewLayout);
        LayoutHelper.ArrangeAndResizeCanvasLayout(previewLayout);
        previewLayout.PreviewSize = previewLayout.CanvasLayout?.CanvasBounds?.OuterBounds.Size
            ?? throw new InvalidOperationException();

        return previewLayout.Build();
    }

    /// <summary>
    /// Positions an arbitrary rectangle (typically a PreviewWindow's own outer bounds) on
    /// the desktop - centered on the activated location.
    /// </summary>
    public static RectangleInfo PositionOnScreen(RectangleInfo bounds, ScreenInfo activatedScreen, PointInfo activatedLocation)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(activatedScreen);
        ArgumentNullException.ThrowIfNull(activatedLocation);

        // center the bounds on the activated location, *but* if the activated location is
        // near the edge of the screen that *could* cause the bounding box to fall partially
        // outside the screen bounds so we'll nudge it back inside the screen bounds as well
        return bounds
            .Center(activatedLocation)
            .MoveInside(activatedScreen.DisplayArea);
    }

    /// <summary>
    /// Derives the box style a hosting window uses to wrap a <see cref="PreviewLayout"/> -
    /// margin and border only (the actual rendered border ring around the preview), with
    /// zero padding since there's no gap between the border's inner edge and the preview
    /// pane's own content - the preview pane's background fills right up to its own edge.
    /// </summary>
    public static BoxStyle GetPreviewWindowStyle(BoxStyle canvasStyle)
    {
        ArgumentNullException.ThrowIfNull(canvasStyle);
        return new BoxStyle(
            marginStyle: canvasStyle.MarginStyle,
            borderStyle: canvasStyle.BorderStyle,
            paddingStyle: PaddingStyle.Empty,
            backgroundStyle: BackgroundStyle.Empty);
    }

    /// <summary>
    /// Derives the box style a <see cref="PreviewLayout"/> uses for its own content - zero
    /// margin and border (both are now the hosting window's job, see
    /// <see cref="GetPreviewWindowStyle"/>) and the padding/background the preview pane renders
    /// itself, inset from its own bounds.
    /// </summary>
    public static BoxStyle GetPreviewPaneStyle(BoxStyle canvasStyle)
    {
        ArgumentNullException.ThrowIfNull(canvasStyle);
        return new BoxStyle(
            marginStyle: MarginStyle.Empty,
            borderStyle: BorderStyle.Empty,
            paddingStyle: canvasStyle.PaddingStyle,
            backgroundStyle: canvasStyle.BackgroundStyle);
    }

    /// <summary>
    /// Takes the size of a PreviewPane's layout and uses it as the baseline for calculating the
    /// containing PreviewWindow's size. Effectively adds the outer border and margin onto the
    /// PreviewPane size.
    /// </summary>
    public static BoxBounds GetPreviewWindowBounds(SizeInfo previewSize, BoxStyle previewWindowStyle)
    {
        ArgumentNullException.ThrowIfNull(previewSize);
        ArgumentNullException.ThrowIfNull(previewWindowStyle);
        var contentBounds = new RectangleInfo(0, 0, previewSize.Width, previewSize.Height);
        return LayoutHelper.GetPreviewWindowBounds(contentBounds, previewWindowStyle);
    }

    /// <summary>
    /// Takes the bounds of a PreviewPane's layout and uses it as the baseline
    /// for calculating the containing PreviewWindow's size. Effectively adds
    /// the outer border and margin onto the PreviewPane size and includes room
    /// for a status bar of given height.
    /// </summary>
    public static BoxBounds GetPreviewWindowBounds(RectangleInfo previewBounds, BoxStyle previewWindowStyle)
    {
        ArgumentNullException.ThrowIfNull(previewBounds);
        ArgumentNullException.ThrowIfNull(previewWindowStyle);
        return BoxBounds.CreateFromContentBounds(
            contentBounds: previewBounds,
            boxStyle: previewWindowStyle);
    }

    /// <summary>
    /// Works out the maximum window and pane sizes for the given constraints, and returns a
    /// <see cref="PreviewLayout.Builder"/> seeded with just <see cref="PreviewLayout.Builder.PreviewSize"/>
    /// and the canvas's own bounds/style - no devices yet, see <see cref="AddDeviceAndScreenStyles"/>.
    /// </summary>
    internal static PreviewLayout.Builder CreateInitialPreviewLayout(
        PreviewStyle previewStyle, BoxStyle previewWindowStyle, BoxStyle previewPaneStyle, SizeInfo? maximumSize = null)
    {
        ArgumentNullException.ThrowIfNull(previewStyle);

        // work out the maximum allowed size of the *window's* outer footprint:
        // * can't be bigger than maximumSize, if the caller gave one (e.g. the activated screen,
        //   for a caller rendering a real on-screen window)
        // * can't be bigger than the configured canvas size
        var windowMaxSize = (maximumSize is not null)
            ? previewStyle.CanvasSize.Clamp(maximumSize)
            : previewStyle.CanvasSize;

        // the preview pane's own maximum size is whatever's left of that footprint once the
        // window's margin/border allowance has been subtracted from it - pure size arithmetic,
        // since PreviewLayout deliberately has no position, only a size (see PreviewLayout).
        var paneMaxSize = windowMaxSize
            .Shrink(previewWindowStyle.MarginStyle)
            .Shrink(previewWindowStyle.BorderStyle);
        var paneMaxBounds = new RectangleInfo(paneMaxSize);

        // this is the root of a nested structure of mutable "builder" objects that can be used
        // to build the final immutable layout objects once all the bounds have been calculated
        return new PreviewLayout.Builder
        {
            PreviewSize = SizeInfo.Empty,
            CanvasLayout = new()
            {
                CanvasBounds = BoxBounds.CreateFromOuterBounds(
                    outerBounds: paneMaxBounds,
                    boxStyle: previewPaneStyle),
                CanvasStyle = previewPaneStyle,
            },
        };
    }

    /// <summary>
    /// Populates <paramref name="previewLayout"/>'s device/screen layouts - one
    /// <see cref="DeviceLayout.Builder"/> per <paramref name="displayInfo"/> device, each with
    /// its screens' styles already resolved (see its own local <c>GetDeviceScreenColor</c>) but their
    /// bounds still <see cref="BoxBounds.Empty"/> placeholders, ready for
    /// <see cref="ArrangeAndScaleDeviceLayouts"/>/<see cref="ArrangeAndScaleScreenLayouts"/> to
    /// arrange for real.
    /// </summary>
    internal static void AddDeviceAndScreenStyles(PreviewLayout.Builder previewLayout, PreviewStyle previewStyle, DisplayInfo displayInfo)
    {
        ArgumentNullException.ThrowIfNull(previewLayout);
        ArgumentNullException.ThrowIfNull(previewStyle);
        ArgumentNullException.ThrowIfNull(displayInfo);

        // check we have at least one device
        if (displayInfo.Devices.Count == 0)
        {
            throw new ArgumentException("Value must contain at least one device.", nameof(displayInfo));
        }

        var canvasLayout = previewLayout.CanvasLayout
            ?? throw new InvalidOperationException();

        canvasLayout.DeviceLayouts = displayInfo.Devices.Select(
            (deviceInfo, deviceIndex) =>
            {
                // the first device (index 0, assumed to be "localhost") gets the configured screen
                // border colour; every other device cycles through ExtraColors (repeating from
                // the start once exhausted), falling back to the configured colour if none are
                // configured.
                var borderColor = (deviceIndex == 0 || previewStyle.ExtraColors.Count == 0)
                    ? previewStyle.ScreenStyle.BorderStyle.Color
                    : previewStyle.ExtraColors[(deviceIndex - 1) % previewStyle.ExtraColors.Count];

                // only create a new ScreenStyle if the border color is different from the
                // configured one - this preserves a reference to the configured ScreenStyle
                // (and its own "ScreenStyle.IsEmpty == true" if the color is Transparent)
                // rather than always allocating a fresh, effectively-identical copy.
                var screenStyle = previewStyle.ScreenStyle.BorderStyle.Color == borderColor
                    ? previewStyle.ScreenStyle
                    : new BoxStyle(
                        previewStyle.ScreenStyle.MarginStyle,
                        previewStyle.ScreenStyle.BorderStyle.WithColor(borderColor),
                        previewStyle.ScreenStyle.PaddingStyle,
                        previewStyle.ScreenStyle.BackgroundStyle);

                return new DeviceLayout.Builder
                {
                    DeviceInfo = deviceInfo,
                    DeviceBounds = BoxBounds.Empty,
                    DeviceStyle = BoxStyle.Empty,
                    ScreenLayouts = deviceInfo.Screens.Select(
                        screenInfo => new ScreenLayout.Builder
                        {
                            ScreenInfo = screenInfo,
                            ScreenBounds = BoxBounds.Empty,
                            ScreenStyle = screenStyle,
                        }).ToList(),
                };
            }).ToList();
    }

    /// <summary>
    /// Arranges the device layouts into a non-overlapping grid and scales them
    /// so the grid fits inside the specified content bounds.
    /// </summary>
    internal static void ArrangeAndScaleDeviceLayouts(PreviewLayout.Builder previewLayout)
    {
        var deviceLayouts = previewLayout.CanvasLayout?.DeviceLayouts
            ?? throw new InvalidOperationException();
        var contentBounds = previewLayout.CanvasLayout.CanvasBounds?.ContentBounds
            ?? throw new InvalidOperationException();

        // build an initial grid of devices at 100% scale. the grid is currently a single row
        // of cells high with all the devices arranged left to right for the time being.
        // we'll enhance this to allow for multiple rows later to support the
        // Mouse Without Borders "square" arrangement.
        var gridRowCount = 1;
        var gridColumnCount = deviceLayouts.Count;
        var deviceGrid = new DeviceLayout.Builder[gridRowCount, gridColumnCount];
        for (var columnIndex = 0; columnIndex < gridColumnCount; columnIndex++)
        {
            deviceGrid[0, columnIndex] = deviceLayouts[columnIndex];
        }

        // if a device has zero screens (or if we failed to connect and read screen layouts) we
        // still want to allocate space in the full size grid so that it doesn't disappear into
        // a zero width or height cell.
        var fullSizeGridCellMinSize = new Size(1024, 768);

        // find the size of the tallest device in each row and the widest device in each column.
        // this tells us how tall each row needs to be and how wide each column needs to be.
        var fullSizeGridCellBounds = new RectangleInfo[gridRowCount, gridColumnCount];
        var fullSizeRowHeights = new decimal[gridRowCount];
        var fullSizeColumnWidths = new decimal[gridColumnCount];
        for (var rowIndex = 0; rowIndex < gridRowCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < gridColumnCount; columnIndex++)
            {
                var deviceInfo = deviceGrid[rowIndex, columnIndex].DeviceInfo ?? throw new InvalidOperationException();
                var deviceBounds = deviceInfo.GetCombinedDisplayArea();

                // if the device bounds are empty, it probably means there aren't any
                // screens available for this device so we'll use the minimum size instead
                // for the grid cell size
                var fullSizeGridCell = deviceBounds.IsEmpty
                    ? deviceBounds.Resize(
                        width: fullSizeGridCellMinSize.Width,
                        height: fullSizeGridCellMinSize.Height)
                    : deviceBounds;

                fullSizeGridCellBounds[rowIndex, columnIndex] = fullSizeGridCell;
                fullSizeRowHeights[rowIndex] = Math.Max(fullSizeRowHeights[rowIndex], fullSizeGridCell.Height);
                fullSizeColumnWidths[columnIndex] = Math.Max(fullSizeColumnWidths[columnIndex], fullSizeGridCell.Width);
            }
        }

        // use the row heights to calculate the absolute coordinates of each grid row
        var fullSizeRowCoordinates = new decimal[gridRowCount];
        fullSizeRowCoordinates[0] = 0;
        for (var rowIndex = 1; rowIndex < gridRowCount; rowIndex++)
        {
            fullSizeRowCoordinates[rowIndex] = fullSizeRowCoordinates[rowIndex - 1] + fullSizeRowHeights[rowIndex - 1];
        }

        // use the column widths to calculate the absolute coordinates of each grid column
        var fullSizeColumnCoordinates = new decimal[gridColumnCount];
        fullSizeColumnCoordinates[0] = 0;
        for (var columnIndex = 1; columnIndex < gridColumnCount; columnIndex++)
        {
            fullSizeColumnCoordinates[columnIndex] = fullSizeColumnCoordinates[columnIndex - 1] + fullSizeColumnWidths[columnIndex - 1];
        }

        // calculate the full size bounds for the entire grid
        var fullSizeGridBounds = new RectangleInfo(
            x: 0,
            y: 0,
            width: fullSizeColumnCoordinates[gridColumnCount - 1] + fullSizeColumnWidths[gridColumnCount - 1],
            height: fullSizeRowCoordinates[gridRowCount - 1] + fullSizeRowHeights[gridRowCount - 1]);

        // work out the scaling factor that will fit the full size grid into the content bounds
        var scaledGridSize = fullSizeGridBounds.Size
            .ScaleToFit(contentBounds.Size, out var scalingFactor);

        // scale the row and column coordinates by the factor. round coordinates to ensure they
        // don't overlap when rendered as pixel dimensions in the image. we'll also add a final
        // "fencepost" coordinate to simplify building the end cell of each row and column in the
        // scaled grid.
        var scaledRowCoordinates = fullSizeRowCoordinates
            .Select(fullSizeRowCoordinate => Math.Round(fullSizeRowCoordinate * scalingFactor))
            .Concat([scaledGridSize.Height])
            .Select(scaledRowCoordinate => scaledRowCoordinate + contentBounds.Top)
            .ToArray();
        var scaledColumnCoordinates = fullSizeColumnCoordinates
            .Select(fullSizeColumnCoordinate => Math.Round(fullSizeColumnCoordinate * scalingFactor))
            .Concat([scaledGridSize.Width])
            .Select(scaledColumnCoordinate => scaledColumnCoordinate + contentBounds.Left)
            .ToArray();

        // arrange the device layouts into their scaled grid positions
        for (var rowIndex = 0; rowIndex < gridRowCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < gridColumnCount; columnIndex++)
            {
                var fullSizeCellSize = fullSizeGridCellBounds[rowIndex, columnIndex].Size;
                if ((fullSizeCellSize.Width == 0) || (fullSizeCellSize.Height == 0))
                {
                    // if the full size bounds are zero width or height, it probably means there aren't any
                    // screens available for this device so we can't scale it to fit inside the grid cell
                    continue;
                }

                // work out the scaled coordinates for this grid cell
                var scaledGridCellBounds = new RectangleInfo(
                    x: scaledColumnCoordinates[columnIndex],
                    y: scaledRowCoordinates[rowIndex],
                    width: scaledColumnCoordinates[columnIndex + 1] - scaledColumnCoordinates[columnIndex],
                    height: scaledRowCoordinates[rowIndex + 1] - scaledRowCoordinates[rowIndex]);

                // scale and center the device layout to fit inside the grid cell
                var deviceLayout = deviceGrid[rowIndex, columnIndex];
                deviceLayout.DeviceBounds = BoxBounds.CreateFromOuterBounds(
                    outerBounds: new RectangleInfo(
                            size: fullSizeCellSize.ScaleToFit(scaledGridCellBounds.Size))
                        .Center(scaledGridCellBounds.Midpoint)
                        .Round()
                        .Intersect(scaledGridCellBounds.Round()),
                    boxStyle: deviceLayout.DeviceStyle);
            }
        }

        // verify that we're pixel-perfect on the positions
        var scaledDeviceBounds = RectangleInfo.Union(
            deviceLayouts
                .Select(deviceLayout => deviceLayout.DeviceBounds.OuterBounds));
        Debug.Assert(
            (scaledDeviceBounds.Width == contentBounds.Width) || (scaledDeviceBounds.Height == contentBounds.Height),
            string.Join(
                "\r\n",
                $"{nameof(ArrangeAndScaleDeviceLayouts)} - scaled device layout does not perfectly fill content bounds:",
                $"scaled grid = '{scaledDeviceBounds}'",
                $"content bounds = '{contentBounds}'"));
    }

    /// <summary>
    /// Arranges the screen layouts inside their respective device cells and
    /// scales them to fit inside their parent device layouts.
    /// </summary>
    internal static void ArrangeAndScaleScreenLayouts(PreviewLayout.Builder previewLayout)
    {
        var deviceLayouts = previewLayout.CanvasLayout?.DeviceLayouts
            ?? throw new InvalidOperationException();

        foreach (var deviceLayout in deviceLayouts)
        {
            var deviceInfo = deviceLayout.DeviceInfo ?? throw new InvalidOperationException();
            var screenLayouts = deviceLayout.ScreenLayouts ?? throw new InvalidOperationException();
            if (screenLayouts.Count == 0)
            {
                // nothing to arrange or scale, and we'll get a divide by zero error
                // in ScaleToFitRatio if we don't quit early
                continue;
            }

            // work out the scaling factor that will fit the screen layouts into the content bounds
            var fullSizeDisplayArea = deviceInfo.GetCombinedDisplayArea();
            var contentBounds = deviceLayout.DeviceBounds.ContentBounds;
            var scalingFactor = fullSizeDisplayArea.Size.ScaleToFitRatio(contentBounds.Size);

            foreach (var screenLayout in screenLayouts)
            {
                var screenInfo = screenLayout.ScreenInfo ?? throw new InvalidOperationException();
                screenLayout.ScreenBounds = BoxBounds.CreateFromOuterBounds(
                    outerBounds: screenInfo.DisplayArea
                        .Offset(-fullSizeDisplayArea.Left, -fullSizeDisplayArea.Top)
                        .Scale(scalingFactor)
                        .Offset(contentBounds.Left, contentBounds.Top)
                        .Round()
                        .Intersect(contentBounds),
                    boxStyle: screenLayout.ScreenStyle);
            }

            // verify that we're pixel-perfect on the positions
            var scaledScreenBounds = RectangleInfo.Union(
                screenLayouts
                    .Select(screenLayout => screenLayout.ScreenBounds.OuterBounds));
            Debug.Assert(
                (scaledScreenBounds.Width == contentBounds.Width) || (scaledScreenBounds.Height == contentBounds.Height),
                string.Join(
                    "\r\n",
                    $"{nameof(ArrangeAndScaleScreenLayouts)} - scaled screen layouts do not perfectly fill content bounds:",
                    $"scaled grid = '{scaledScreenBounds}'",
                    $"content bounds = '{contentBounds}'"));
        }
    }

    /// <summary>
    /// Crops the layout's canvas bounds to tightly wrap the contained device layouts
    /// and moves it back to the same origin it started at.
    /// </summary>
    internal static void ArrangeAndResizeCanvasLayout(PreviewLayout.Builder previewLayout)
    {
        var canvasLayout = previewLayout.CanvasLayout ?? throw new InvalidOperationException();

        // work out how big the canvas needs to be in order to contain the device layouts
        var canvasContentBounds = RectangleInfo.Union(
            (canvasLayout.DeviceLayouts ?? throw new InvalidOperationException())
                .Select(deviceLayout => deviceLayout.DeviceBounds.OuterBounds));
        var canvasOuterBounds = BoxBounds.CreateFromContentBounds(
            contentBounds: canvasContentBounds,
            boxStyle: canvasLayout.CanvasStyle);

        // move the canvas into position
        var positionedOuterBounds = canvasOuterBounds.OuterBounds.MoveTo(
            (canvasLayout.CanvasBounds ?? throw new InvalidOperationException())
                .OuterBounds.Location);

        canvasLayout.CanvasBounds = BoxBounds.CreateFromOuterBounds(
            outerBounds: positionedOuterBounds,
            boxStyle: canvasLayout.CanvasStyle);
    }
}

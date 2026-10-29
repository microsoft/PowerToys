// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;

using Windows.System;

namespace MouseJump.WinUI3.UI;

internal sealed partial class PreviewPane
{
    private void PreviewPane_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Pointer.PointerDeviceType.Equals(PointerDeviceType.Mouse))
        {
            // not a mouse click
            return;
        }

        var pointerPoint = e.GetCurrentPoint(this);
        if (pointerPoint.Properties.IsRightButtonPressed)
        {
            this.Cancel?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var layout = this.Layout;
        if (layout is null)
        {
            return;
        }

        // the pointer position is in DIPs, relative to this control - scale it up to the
        // same physical-pixel space that Layout's bounds are expressed in
        var scale = this.GetRasterizationScale();
        var pointerLocation = new PointInfo((decimal)pointerPoint.Position.X, (decimal)pointerPoint.Position.Y)
            .Scale((decimal)scale);

        // work out which screenshot was clicked, keeping the owning device too
        var clickedEntry = layout.CanvasLayout.DeviceLayouts
            .SelectMany(deviceLayout => deviceLayout.ScreenLayouts.Select(
                screenLayout => (DeviceLayout: deviceLayout, ScreenLayout: screenLayout)))
            .SingleOrDefault(
                entry => entry.ScreenLayout.ScreenBounds.OuterBounds.Contains(pointerLocation));
        if (clickedEntry.ScreenLayout is null)
        {
            return;
        }

        // scale up the click onto the physical screen - the aspect ratio of the screenshot
        // might be distorted compared to the physical screen due to the borders around the
        // screenshot, so we need to work out the target location on the physical screen first
        var clickedDisplayArea = clickedEntry.ScreenLayout.ScreenInfo.DisplayArea;
        var clickedLocation = pointerLocation
            .Stretch(
                source: clickedEntry.ScreenLayout.ScreenBounds.ContentBounds,
                target: clickedDisplayArea)
            .Clamp(
                new(
                    x: clickedDisplayArea.X + 1,
                    y: clickedDisplayArea.Y + 1,
                    width: clickedDisplayArea.Width - 1,
                    height: clickedDisplayArea.Height - 1))
            .Truncate();

        this.NavigateTo?.Invoke(
            this, new NavigateToEventArgs(clickedEntry.DeviceLayout.DeviceInfo, clickedLocation));
    }

    /// <summary>
    /// Maps 1-9/numpad/P/Left/Right/Home/End to <see cref="NavigateTo"/> and Escape to
    /// <see cref="Cancel"/>, entirely from <see cref="Layout"/> and <see cref="ActiveScreen"/> -
    /// no live desktop/cursor state needed. Screen numbering and Home/End use the flattened
    /// device/screen order (<see cref="GetOrderedScreens"/>), which matches
    /// <c>ScreenHelper.GetAllScreens()</c>'s order since <c>LayoutHelper.GetPreviewLayout</c>
    /// builds <see cref="Layout"/> from that same list via order-preserving projections.
    /// </summary>
    private void PreviewPane_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            this.Cancel?.Invoke(this, EventArgs.Empty);
            return;
        }

        var layout = this.Layout;
        var activeScreen = this.ActiveScreen;
        if (layout is null || activeScreen is null)
        {
            return;
        }

        var orderedScreens = this.GetOrderedScreens(layout);
        if (orderedScreens.Count == 0)
        {
            return;
        }

        var currentIndex = orderedScreens.FindIndex(
            entry => entry.ScreenLayout.ScreenInfo.Equals(activeScreen));

        var target = e.Key switch
        {
            >= VirtualKey.Number1 and <= VirtualKey.Number9 =>
                PreviewPane.ElementAtOrDefault(orderedScreens, (e.Key - VirtualKey.Number0) - 1),
            >= VirtualKey.NumberPad1 and <= VirtualKey.NumberPad9 =>
                PreviewPane.ElementAtOrDefault(orderedScreens, (e.Key - VirtualKey.NumberPad0) - 1),
            VirtualKey.P =>
                orderedScreens.SingleOrDefault(entry => entry.ScreenLayout.ScreenInfo.Primary),
            VirtualKey.Left when currentIndex >= 0 =>
                orderedScreens[(currentIndex - 1 + orderedScreens.Count) % orderedScreens.Count],
            VirtualKey.Right when currentIndex >= 0 =>
                orderedScreens[(currentIndex + 1) % orderedScreens.Count],
            VirtualKey.Home => orderedScreens[0],
            VirtualKey.End => orderedScreens[^1],
            _ => default,
        };

        if (target.ScreenLayout is null)
        {
            return;
        }

        this.NavigateTo?.Invoke(
            this,
            new NavigateToEventArgs(
                target.DeviceLayout.DeviceInfo, target.ScreenLayout.ScreenInfo.DisplayArea.Midpoint));
    }

    private List<(DeviceLayout DeviceLayout, ScreenLayout ScreenLayout)> GetOrderedScreens(PreviewLayout layout)
        => layout.CanvasLayout.DeviceLayouts
            .SelectMany(deviceLayout => deviceLayout.ScreenLayouts.Select(
                screenLayout => (DeviceLayout: deviceLayout, ScreenLayout: screenLayout)))
            .ToList();

    private static (DeviceLayout DeviceLayout, ScreenLayout ScreenLayout) ElementAtOrDefault(
        List<(DeviceLayout DeviceLayout, ScreenLayout ScreenLayout)> screens, int index)
        => (index >= 0 && index < screens.Count) ? screens[index] : default;
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;

namespace Microsoft.PowerToys.Settings.UI.Controls;

[TemplatePart(Name = nameof(PART_FooterPresenter), Type = typeof(ContentPresenter))]
[TemplatePart(Name = nameof(PART_ContentPresenter), Type = typeof(ContentPresenter))]

public partial class TitleBar : Control
{
#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1400 // Access modifier should be declared
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    ContentPresenter? PART_ContentPresenter;
    ContentPresenter? PART_FooterPresenter;
    #pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#pragma warning restore SA1400 // Access modifier should be declared
#pragma warning restore SA1306 // Field names should begin with lower-case letter
#pragma warning restore SA1310 // Field names should not contain underscore

    private void SetWASDKTitleBar()
    {
        if (this.Window == null)
        {
            return;
        }

        if (AutoConfigureCustomTitleBar)
        {
            Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            this.Window.SizeChanged -= Window_SizeChanged;
            this.Window.SizeChanged += Window_SizeChanged;
            this.Window.Activated -= Window_Activated;
            this.Window.Activated += Window_Activated;

            if (Window.Content is FrameworkElement rootElement)
            {
                UpdateCaptionButtons(rootElement);
                rootElement.ActualThemeChanged += (s, e) =>
                {
                    UpdateCaptionButtons(rootElement);
                };
            }

            PART_ContentPresenter = GetTemplateChild(nameof(PART_ContentPresenter)) as ContentPresenter;
            PART_FooterPresenter = GetTemplateChild(nameof(PART_FooterPresenter)) as ContentPresenter;

            // Get caption button occlusion information.
            int captionButtonOcclusionWidthRight = Window.AppWindow.TitleBar.RightInset;
            int captionButtonOcclusionWidthLeft = Window.AppWindow.TitleBar.LeftInset;
            PART_LeftPaddingColumn!.Width = new GridLength(captionButtonOcclusionWidthLeft);
            PART_RightPaddingColumn!.Width = new GridLength(captionButtonOcclusionWidthRight);

            if (DisplayMode == DisplayMode.Tall)
            {
                // Choose a tall title bar to provide more room for interactive elements
                // like search box or person picture controls.
                Window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }
            else
            {
                Window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }

            // Recalculate the drag region for the custom title bar
            // if you explicitly defined new draggable areas.
            SetDragRegionForCustomTitleBar();

            _isAutoConfigCompleted = true;
        }
    }

    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        UpdateVisualStateAndDragRegion(args.Size);
    }

    private void UpdateCaptionButtons(FrameworkElement rootElement)
    {
        Window.AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        Window.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        if (rootElement.ActualTheme == ElementTheme.Dark)
        {
            Window.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
            Window.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
        }
        else
        {
            Window.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
            Window.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
        }
    }

    private void ResetWASDKTitleBar()
    {
        if (this.Window == null)
        {
            return;
        }

        // Only reset if we were the ones who configured
        if (_isAutoConfigCompleted)
        {
            Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
            this.Window.SizeChanged -= Window_SizeChanged;
            this.Window.Activated -= Window_Activated;
            SizeChanged -= this.TitleBar_SizeChanged;
            Window.AppWindow.TitleBar.ResetToDefault();
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            VisualStateManager.GoToState(this, WindowDeactivatedState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, WindowActivatedState, true);
        }
    }

    public void SetDragRegionForCustomTitleBar()
    {
        if (AutoConfigureCustomTitleBar && Window is not null)
        {
            ClearDragRegions(NonClientRegionKind.Passthrough);
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
            var items = new FrameworkElement?[] { PART_ContentPresenter, PART_FooterPresenter, PART_ButtonHolder };
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
            var validItems = items.Where(x => x is not null).Select(x => x!).ToArray(); // Prune null items

            SetDragRegion(NonClientRegionKind.Passthrough, validItems);
        }
    }

    public double GetRasterizationScaleForElement(UIElement element)
    {
        if (element.XamlRoot != null)
        {
            return element.XamlRoot.RasterizationScale;
        }

        return 0.0;
    }

    public void SetDragRegion(NonClientRegionKind nonClientRegionKind, params FrameworkElement[] frameworkElements)
    {
        List<RectInt32> rects = new List<RectInt32>();
        var scale = GetRasterizationScaleForElement(this);

        foreach (var frameworkElement in frameworkElements)
        {
            if (frameworkElement == null)
            {
                continue;
            }

            GeneralTransform transformElement = frameworkElement.TransformToVisual(null);
            Rect bounds = transformElement.TransformBounds(new Rect(0, 0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));
            var transparentRect = new RectInt32(
                _X: (int)Math.Round(bounds.X * scale),
                _Y: (int)Math.Round(bounds.Y * scale),
                _Width: (int)Math.Round(bounds.Width * scale),
                _Height: (int)Math.Round(bounds.Height * scale));
            rects.Add(transparentRect);
        }

        if (rects.Count > 0)
        {
            InputNonClientPointerSource.GetForWindowId(Window.AppWindow.Id).SetRegionRects(nonClientRegionKind, rects.ToArray());
        }
    }

    public void ClearDragRegions(NonClientRegionKind nonClientRegionKind)
    {
        InputNonClientPointerSource.GetForWindowId(Window.AppWindow.Id).ClearRegionRects(nonClientRegionKind);
    }
}

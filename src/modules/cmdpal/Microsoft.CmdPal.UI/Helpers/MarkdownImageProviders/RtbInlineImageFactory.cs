// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

/// <summary>
/// Creates a new image configured to behave well as an inline image in a RichTextBlock.
/// </summary>
internal static class RtbInlineImageFactory
{
    public sealed class InlineImageOptions
    {
        public double? WidthDip { get; init; }

        public double? HeightDip { get; init; }

        public double? MaxWidthDip { get; init; }

        public double? MaxHeightDip { get; init; }

        public bool FitColumnWidth { get; init; } = true;

        public bool DownscaleOnly { get; init; } = true;

        public Stretch Stretch { get; init; } = Stretch.None;
    }

    internal static Image Create(ImageSource source, InlineImageOptions? options = null)
    {
        options ??= new InlineImageOptions();

        var img = new Image
        {
            Source = source,
            Stretch = options.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Track host RTB and subscribe once
        RichTextBlock? rtb = null;
        long paddingToken = 0;
        var paddingSubscribed = false;

        SizeChangedEventHandler? rtbSizeChangedHandler = null;
        TypedEventHandler<XamlRoot, XamlRootChangedEventArgs>? xamlRootChangedHandler = null;

        // If Source is replaced later, recompute
        var sourceToken = img.RegisterPropertyChangedCallback(Image.SourceProperty!, (_, __) => Update());

        img.Loaded += OnLoaded;
        img.Unloaded += OnUnloaded;
        img.ImageOpened += (_, __) => Update();

        return img;

        void OnLoaded(object? s, RoutedEventArgs e)
        {
            // store image initial Width and Height if they are force upon the image by
            // MarkdownControl itself as result of parsing <img width="123" height="123" />
            // If user sets Width/Height in options, that takes precedence
            img.Tag ??= (img.Width, img.Height);

            rtb ??= FindAncestor<RichTextBlock>(img);
            if (rtb != null && !paddingSubscribed)
            {
                rtbSizeChangedHandler ??= (_, __) => Update();
                rtb.SizeChanged += rtbSizeChangedHandler;

                paddingToken = rtb.RegisterPropertyChangedCallback(Control.PaddingProperty!, (_, __) => Update());
                paddingSubscribed = true;
            }

            if (img.XamlRoot != null)
            {
                xamlRootChangedHandler ??= (_, __) => Update();
                img.XamlRoot.Changed += xamlRootChangedHandler;
            }

            Update();
        }

        void OnUnloaded(object? s, RoutedEventArgs e)
        {
            if (rtb != null && rtbSizeChangedHandler is not null)
            {
                rtb.SizeChanged -= rtbSizeChangedHandler;
            }

            if (rtb != null && paddingSubscribed)
            {
                rtb.UnregisterPropertyChangedCallback(Control.PaddingProperty!, paddingToken);
                paddingSubscribed = false;
            }

            if (img.XamlRoot != null && xamlRootChangedHandler is not null)
            {
                img.XamlRoot.Changed -= xamlRootChangedHandler;
            }

            img.UnregisterPropertyChangedCallback(Image.SourceProperty!, sourceToken);
        }

        void Update()
        {
            if (rtb is null)
            {
                return;
            }

            double? externalWidth = null;
            double? externalHeight = null;
            if (img.Tag != null)
            {
                (externalWidth, externalHeight) = ((double, double))img.Tag;
            }

            var pad = rtb.Padding;
            var columnDip = Math.Max(0.0, rtb.ActualWidth - pad.Left - pad.Right);
            var scale = img.XamlRoot?.RasterizationScale is double s1 and > 0 ? s1 : 1.0;

            var isSvg = img.Source is SvgImageSource;
            var naturalPxW = GetNaturalPixelWidth(img.Source);
            var naturalDipW = naturalPxW > 0 && naturalPxW != int.MaxValue ? naturalPxW / scale : double.PositiveInfinity; // SVG => ∞

            double? desiredWidth = null;
            if (externalWidth.HasValue && !double.IsNaN(externalWidth.Value))
            {
                img.Width = externalWidth.Value;
                desiredWidth = externalWidth.Value;
            }
            else
            {
                if (options.WidthDip is double forcedW)
                {
                    desiredWidth = options.DownscaleOnly && naturalPxW != int.MaxValue
                        ? Math.Min(forcedW, naturalPxW)
                        : forcedW;
                }
                else if (options.FitColumnWidth)
                {
                    desiredWidth = options.DownscaleOnly && naturalPxW != int.MaxValue
                        ? Math.Min(columnDip, naturalPxW)
                        : columnDip;
                }
                else
                {
                    desiredWidth = naturalPxW;
                }

                // Apply MaxWidth (never exceed column width by default)
                double maxW;
                var maxConstraint = options.FitColumnWidth ? columnDip : (isSvg ? 256 : double.PositiveInfinity);
                if (options.MaxWidthDip.HasValue)
                {
                    maxW = Math.Min(options.MaxWidthDip.Value, maxConstraint);
                }
                else if (options.DownscaleOnly)
                {
                    maxW = Math.Min(naturalPxW, maxConstraint);
                }
                else
                {
                    maxW = maxConstraint;
                }

                // Commit sizes
                if (desiredWidth is double w)
                {
                    img.Width = Math.Max(0, w);
                }

                img.MaxWidth = maxW is double mwv && mwv > 0 ? mwv : maxConstraint;
            }

            if (externalHeight.HasValue && !double.IsNaN(externalHeight.Value))
            {
                img.Height = externalHeight.Value;
            }
            else
            {
                // ---- Height & MaxHeight ----
                var desiredHeight = options.HeightDip;
                var maxH = options.MaxHeightDip;

                if (desiredHeight is double h)
                {
                    img.Height = Math.Max(0, h);
                }

                if (maxH is double mh && mh > 0)
                {
                    img.MaxHeight = mh;
                }
            }

            if (options.FitColumnWidth
                || options.WidthDip is not null
                || options.HeightDip is not null
                || options.MaxWidthDip is not null
                || options.MaxHeightDip is not null
                || externalWidth.HasValue
                || externalHeight.HasValue)
            {
                img.Stretch = Stretch.Uniform;
            }
            else
            {
                img.Stretch = Stretch.None;
            }

            // Decode/rasterization hints
            if (isSvg && img.Source is SvgImageSource svg)
            {
                var targetW = desiredWidth ?? Math.Min(img.MaxWidth, columnDip);
                var pxW = Math.Max(1, (int)Math.Round(targetW * scale));
                if ((int)svg.RasterizePixelWidth != pxW)
                {
                    svg.RasterizePixelWidth = pxW;
                }

                if (options.HeightDip is double forcedH)
                {
                    var pxH = Math.Max(1, (int)Math.Round(forcedH * scale));
                    if ((int)svg.RasterizePixelHeight != pxH)
                    {
                        svg.RasterizePixelHeight = pxH;
                    }
                }
            }
            else if (img.Source is BitmapImage bi && naturalPxW > 0)
            {
                var widthToUse = desiredWidth ?? Math.Min(img.MaxWidth, columnDip);
                if (widthToUse > 0)
                {
                    var desiredPx = (int)Math.Round(Math.Min(naturalPxW, widthToUse * scale));
                    if (desiredPx > 0 && bi.DecodePixelWidth != desiredPx)
                    {
                        bi.DecodePixelWidth = desiredPx;
                    }
                }
            }
        }
    }

    private static int GetNaturalPixelWidth(ImageSource? src) => src switch
    {
        BitmapSource bs when bs.PixelWidth > 0 => bs.PixelWidth, // raster
        SvgImageSource sis => sis.RasterizePixelWidth > 0 ? (int)sis.RasterizePixelWidth : int.MaxValue, // vector => infinite
        _ => 0,
    };

    private static T? FindAncestor<T>(DependencyObject start)
        where T : DependencyObject
    {
        var cur = (DependencyObject?)start;
        while (cur != null)
        {
            cur = VisualTreeHelper.GetParent(cur);
            if (cur is T hit)
            {
                return hit;
            }
        }

        return null;
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TopToolbar.Models;
using TopToolbar.Services;
using Windows.UI;

namespace TopToolbar.Helpers
{
    internal static class IconElementFactory
    {
        private const string DefaultGlyph = "\uE10F";

        public static FrameworkElement Create(ToolbarButton model, double size, double dpiScale, Color? foregroundOverride = null)
        {
            if (model == null)
            {
                return CreateGlyph(DefaultGlyph, size, foregroundOverride);
            }

            return model.IconType switch
            {
                ToolbarIconType.Image => CreateImageIcon(model, size, dpiScale, foregroundOverride),
                _ => CreateCatalogIcon(model, size, dpiScale, foregroundOverride),
            };
        }

        private static FrameworkElement CreateCatalogIcon(ToolbarButton model, double size, double dpiScale, Color? foregroundOverride)
        {
            var entry = IconCatalogService.ResolveFromPath(model.IconPath) ?? IconCatalogService.GetDefault();
            if (entry == null)
            {
                return CreateGlyph(model.IconGlyph, size, foregroundOverride);
            }

            if (entry.HasGlyph)
            {
                var color = foregroundOverride ?? Color.FromArgb(255, 255, 255, 255);
                var fontFamily = string.IsNullOrWhiteSpace(entry.FontFamily)
                    ? new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets")
                    : new FontFamily(entry.FontFamily);

                return new FontIcon
                {
                    Glyph = entry.Glyph,
                    FontFamily = fontFamily,
                    FontSize = size * 0.9,
                    Width = size,
                    Height = size,
                    Foreground = new SolidColorBrush(color),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }

            if (!entry.HasImage || entry.ResourceUri == null)
            {
                return CreateGlyph(model.IconGlyph, size, foregroundOverride);
            }

            var svg = new SvgImageSource(entry.ResourceUri)
            {
                RasterizePixelWidth = Math.Max(size * dpiScale, size),
                RasterizePixelHeight = Math.Max(size * dpiScale, size),
            };

            return new Image
            {
                Width = size,
                Height = size,
                Source = svg,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private static FrameworkElement CreateImageIcon(ToolbarButton model, double size, double dpiScale, Color? foregroundOverride)
        {
            var path = model.IconPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return CreateGlyph(model.IconGlyph, size, foregroundOverride);
            }

            if (IconCatalogService.TryParseCatalogId(path, out var catalogId) && IconCatalogService.TryGetById(catalogId, out var catalogEntry))
            {
                var clonedModel = model.Clone();
                clonedModel.IconType = ToolbarIconType.Catalog;
                clonedModel.IconPath = IconCatalogService.BuildCatalogPath(catalogEntry.Id);
                return CreateCatalogIcon(clonedModel, size, dpiScale, foregroundOverride);
            }

            double rasterSize = Math.Max(size * dpiScale, size);

            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                var svgSource = new SvgImageSource
                {
                    RasterizePixelWidth = rasterSize,
                    RasterizePixelHeight = rasterSize,
                };

                if (TryCreateUri(path, out var svgUri))
                {
                    svgSource.UriSource = svgUri;
                }
                else if (File.Exists(path))
                {
                    svgSource.UriSource = new Uri(path);
                }
                else
                {
                    return CreateGlyph(model.IconGlyph, size, foregroundOverride);
                }

                return new Image
                {
                    Width = size,
                    Height = size,
                    Source = svgSource,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }

            var bitmap = new BitmapImage
            {
                DecodePixelWidth = (int)Math.Round(rasterSize),
                DecodePixelHeight = (int)Math.Round(rasterSize),
            };

            if (TryCreateUri(path, out var uri))
            {
                bitmap.UriSource = uri;
            }
            else if (File.Exists(path))
            {
                bitmap.UriSource = new Uri(path);
            }
            else
            {
                return CreateGlyph(model.IconGlyph, size, foregroundOverride);
            }

            return new Image
            {
                Width = size,
                Height = size,
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private static FrameworkElement CreateGlyph(string glyph, double size, Color? foregroundOverride)
        {
            var actualGlyph = string.IsNullOrWhiteSpace(glyph) ? DefaultGlyph : glyph.Trim();
            var color = foregroundOverride ?? Color.FromArgb(255, 255, 255, 255);
            return new FontIcon
            {
                Glyph = actualGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = size,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private static bool TryCreateUri(string path, out Uri uri)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out uri))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                var expanded = Environment.ExpandEnvironmentVariables(path);
                if (Uri.TryCreate(expanded, UriKind.Absolute, out uri))
                {
                    return true;
                }
            }

            uri = null;
            return false;
        }
    }
}

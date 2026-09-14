// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ScreenTranslator.Core.Translation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ScreenTranslator.Core.Layout;

public static class OverlayAppearanceHelper
{
    private const int BytesPerPixel = 4;
    private const double StableBackgroundDeviation = 35.0;

    public static IReadOnlyList<TranslatedLine> ApplySampledAppearance(
        SoftwareBitmap capturedBitmap,
        PhysicalRect capturedRegion,
        IReadOnlyList<TranslatedLine> lines)
    {
        ArgumentNullException.ThrowIfNull(capturedBitmap);
        ArgumentNullException.ThrowIfNull(lines);

        if (capturedBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            return lines;
        }

        byte[] pixels = ReadPixels(capturedBitmap);
        List<TranslatedLine> styledLines = new(lines.Count);

        foreach (TranslatedLine line in lines)
        {
            uint? background = TrySampleBackground(
                pixels,
                capturedBitmap.PixelWidth,
                capturedBitmap.PixelHeight,
                capturedRegion,
                line.BoundingBox);
            uint? foreground = background.HasValue
                ? TrySampleForeground(
                    pixels,
                    capturedBitmap.PixelWidth,
                    capturedBitmap.PixelHeight,
                    capturedRegion,
                    line.BoundingBox,
                    background.Value)
                : null;

            styledLines.Add(background.HasValue
                ? line with
                {
                    OverlayBackgroundColorArgb = background,
                    OverlayForegroundColorArgb = foreground ?? GetContrastingTextColorArgb(background.Value),
                }
                : line);
        }

        return styledLines;
    }

    private static uint? TrySampleForeground(
            byte[] pixels,
            int pixelWidth,
            int pixelHeight,
            PhysicalRect capturedRegion,
            PhysicalRect textRegion,
            uint backgroundArgb)
        {
            int left = ClampToPixel(textRegion.Left - capturedRegion.Left, 0, pixelWidth - 1);
            int top = ClampToPixel(textRegion.Top - capturedRegion.Top, 0, pixelHeight - 1);
            int right = ClampToPixel(textRegion.Right - capturedRegion.Left, left + 1, pixelWidth);
            int bottom = ClampToPixel(textRegion.Bottom - capturedRegion.Top, top + 1, pixelHeight);

            double backgroundLuminance = GetLuminance(backgroundArgb);
            List<(byte Red, byte Green, byte Blue)> candidates = new();
            for (int y = top; y < bottom; y += Math.Max(1, (bottom - top) / 16))
            {
                for (int x = left; x < right; x += Math.Max(1, (right - left) / 16))
                {
                    var pixel = ReadPixel(pixels, pixelWidth, x, y);
                    uint color = ToArgb(pixel.Red, pixel.Green, pixel.Blue);
                    if (Math.Abs(GetLuminance(color) - backgroundLuminance) >= 0.14)
                    {
                        candidates.Add(pixel);
                    }
                }
            }

            if (candidates.Count < 4)
            {
                return null;
            }

            byte red = (byte)Math.Clamp((int)Math.Round(candidates.Average(sample => sample.Red)), 0, 255);
            byte green = (byte)Math.Clamp((int)Math.Round(candidates.Average(sample => sample.Green)), 0, 255);
            byte blue = (byte)Math.Clamp((int)Math.Round(candidates.Average(sample => sample.Blue)), 0, 255);
            uint sampledColor = ToArgb(red, green, blue);

            return HasReadableContrast(sampledColor, backgroundArgb) ? sampledColor : null;
        }

    public static uint GetContrastingTextColorArgb(uint backgroundArgb)
    {
        double red = ((backgroundArgb >> 16) & 0xFF) / 255.0;
        double green = ((backgroundArgb >> 8) & 0xFF) / 255.0;
        double blue = (backgroundArgb & 0xFF) / 255.0;

        red = ToLinear(red);
        green = ToLinear(green);
        blue = ToLinear(blue);

        double luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        return luminance > 0.45 ? 0xFF000000u : 0xFFFFFFFFu;
    }

    private static uint? TrySampleBackground(
        byte[] pixels,
        int pixelWidth,
        int pixelHeight,
        PhysicalRect capturedRegion,
        PhysicalRect textRegion)
    {
        int left = ClampToPixel(textRegion.Left - capturedRegion.Left, 0, pixelWidth - 1);
        int top = ClampToPixel(textRegion.Top - capturedRegion.Top, 0, pixelHeight - 1);
        int right = ClampToPixel(textRegion.Right - capturedRegion.Left, left + 1, pixelWidth);
        int bottom = ClampToPixel(textRegion.Bottom - capturedRegion.Top, top + 1, pixelHeight);

        int padding = Math.Max(2, Math.Min(8, Math.Min(right - left, bottom - top) / 4));
        List<(byte Red, byte Green, byte Blue)> samples = new();

        AddHorizontalSamples(samples, pixels, pixelWidth, left, right, top - padding, pixelHeight);
        AddHorizontalSamples(samples, pixels, pixelWidth, left, right, bottom + padding, pixelHeight);
        AddVerticalSamples(samples, pixels, pixelWidth, left - padding, top, bottom, pixelWidth, pixelHeight);
        AddVerticalSamples(samples, pixels, pixelWidth, right + padding, top, bottom, pixelWidth, pixelHeight);

        if (samples.Count < 4)
        {
            return null;
        }

        double redAverage = 0;
        double greenAverage = 0;
        double blueAverage = 0;
        foreach (var sample in samples)
        {
            redAverage += sample.Red;
            greenAverage += sample.Green;
            blueAverage += sample.Blue;
        }

        redAverage /= samples.Count;
        greenAverage /= samples.Count;
        blueAverage /= samples.Count;

        double deviation = 0;
        foreach (var sample in samples)
        {
            deviation += Math.Sqrt(
                Math.Pow(sample.Red - redAverage, 2) +
                Math.Pow(sample.Green - greenAverage, 2) +
                Math.Pow(sample.Blue - blueAverage, 2));
        }

        if (deviation / samples.Count > StableBackgroundDeviation)
        {
            return null;
        }

        return 0xFF000000u |
            ((uint)Math.Clamp((int)Math.Round(redAverage), 0, 255) << 16) |
            ((uint)Math.Clamp((int)Math.Round(greenAverage), 0, 255) << 8) |
            (uint)Math.Clamp((int)Math.Round(blueAverage), 0, 255);
    }

    private static void AddHorizontalSamples(
        List<(byte Red, byte Green, byte Blue)> samples,
        byte[] pixels,
        int pixelWidth,
        int left,
        int right,
        int y,
        int pixelHeight)
    {
        if (y < 0 || y >= pixelHeight)
        {
            return;
        }

        int step = Math.Max(1, (right - left) / 8);
        for (int x = left; x < right; x += step)
        {
            samples.Add(ReadPixel(pixels, pixelWidth, x, y));
        }
    }

    private static void AddVerticalSamples(
        List<(byte Red, byte Green, byte Blue)> samples,
        byte[] pixels,
        int pixelWidth,
        int x,
        int top,
        int bottom,
        int imageWidth,
        int imageHeight)
    {
        if (x < 0 || x >= imageWidth)
        {
            return;
        }

        int step = Math.Max(1, (bottom - top) / 8);
        for (int y = top; y < bottom; y += step)
        {
            samples.Add(ReadPixel(pixels, pixelWidth, x, y));
        }
    }

    private static (byte Red, byte Green, byte Blue) ReadPixel(byte[] pixels, int pixelWidth, int x, int y)
    {
        int offset = ((y * pixelWidth) + x) * BytesPerPixel;
        return (pixels[offset + 2], pixels[offset + 1], pixels[offset]);
    }

    private static byte[] ReadPixels(SoftwareBitmap bitmap)
    {
        uint capacity = (uint)(bitmap.PixelWidth * bitmap.PixelHeight * BytesPerPixel);
        Windows.Storage.Streams.Buffer buffer = new(capacity);
        bitmap.CopyToBuffer(buffer);

        byte[] pixels = new byte[capacity];
        using DataReader reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(pixels);
        return pixels;
    }

    private static int ClampToPixel(double value, int minimum, int maximum)
    {
        return Math.Clamp((int)Math.Round(value), minimum, maximum);
    }

    private static double ToLinear(double value)
    {
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static uint ToArgb(byte red, byte green, byte blue)
    {
        return 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | blue;
    }

    private static double GetLuminance(uint argb)
    {
        double red = ToLinear(((argb >> 16) & 0xFF) / 255.0);
        double green = ToLinear(((argb >> 8) & 0xFF) / 255.0);
        double blue = ToLinear((argb & 0xFF) / 255.0);
        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static bool HasReadableContrast(uint foregroundArgb, uint backgroundArgb)
    {
        double foreground = GetLuminance(foregroundArgb);
        double background = GetLuminance(backgroundArgb);
        double lighter = Math.Max(foreground, background);
        double darker = Math.Min(foreground, background);
        return (lighter + 0.05) / (darker + 0.05) >= 3.0;
    }
}

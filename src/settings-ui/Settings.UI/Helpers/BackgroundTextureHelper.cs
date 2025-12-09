// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

/// <summary>
/// Helper to generate textures at runtime.
/// </summary>
public static class BackgroundTextureHelper
{
    public static async Task<ImageSource> CreateCornerGradientAsync(
        Color topLeft, Color topRight, Color bottomLeft, Color bottomRight)
    {
        int width = 2;
        int height = 2;
        byte[] pixelData = new byte[width * height * 4]; // BGRA8 format

        void SetPixel(int x, int y, Color color)
        {
            int index = ((y * width) + x) * 4;
            pixelData[index + 0] = color.B;
            pixelData[index + 1] = color.G;
            pixelData[index + 2] = color.R;
            pixelData[index + 3] = color.A;
        }

        SetPixel(0, 0, topLeft);
        SetPixel(1, 0, topRight);
        SetPixel(0, 1, bottomLeft);
        SetPixel(1, 1, bottomRight);

        var writeableBitmap = new WriteableBitmap(width, height);
        using var pixelStream = writeableBitmap.PixelBuffer.AsStream();
        await pixelStream.WriteAsync(pixelData);

        return writeableBitmap;
    }

    public static ImageSource CreateNoiseTexture(int width, int height, byte intensity = 64)
    {
        var random = new Random();
        var writeableBitmap = new WriteableBitmap(width, height);

        using var pixelStream = writeableBitmap.PixelBuffer.AsStream();

        Span<byte> pixels = new byte[width * 4];

        for (int y = 0; y < height; y++)
        {
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte grey = (byte)random.Next(0, intensity);
                pixels[i + 0] = grey; // B
                pixels[i + 1] = grey; // G
                pixels[i + 2] = grey; // R
                pixels[i + 3] = 255;  // A
            }

            pixelStream.Write(pixels);
        }

        return writeableBitmap;
    }

    public static ImageBrush CreateNoiseBrush(int outputWidth, int outputHeight, double opacity = 1.0) =>
        new()
        {
            ImageSource = CreateNoiseTexture(outputWidth, outputHeight, intensity: 50),
            Stretch = Stretch.None,
            Opacity = opacity,
        };
}

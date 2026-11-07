// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Gives the notice its shape: the rounded card and its arrow, with an outline and a flyout's soft shadow, as the
/// premultiplied per-pixel alpha a layered window takes. The text is already drawn on the card's color; this keeps it
/// inside the shape and makes everything else transparent but the shadow. Pure, so tests cover it without a window.
/// </summary>
internal static class NoticeRenderer
{
    private const float Sqrt2 = 1.4142135f;

    /// <summary>The card and its arrow, in pixels from the bitmap's top-left corner.</summary>
    /// <param name="ArrowX">The arrow's tip, across.</param>
    /// <param name="ArrowY">The arrow's tip, down.</param>
    /// <param name="ArrowUp">Whether the arrow points up, from the top of the card, or down, from its bottom.</param>
    /// <param name="Border">How thick the outline is.</param>
    internal readonly record struct Shape(
        float Left,
        float Top,
        float Right,
        float Bottom,
        float Radius,
        float ArrowX,
        float ArrowY,
        float ArrowHalfWidth,
        float ArrowHeight,
        bool ArrowUp,
        float Border);

    /// <summary>A layer of shadow: the shape, moved down by <paramref name="OffsetY"/> and blurred.</summary>
    /// <param name="OffsetY">How far down the shadow falls.</param>
    /// <param name="Sigma">How soft it is: a CSS blur radius of 16 is a sigma of 8.</param>
    /// <param name="Opacity">How dark it is under the shape.</param>
    internal readonly record struct Shadow(float OffsetY, float Sigma, float Opacity);

    /// <summary>
    /// Turns <paramref name="pixels"/>, the card's color with the text drawn on it, into the notice: what's inside the
    /// shape, its outline in <paramref name="border"/> (0xRRGGBB), the shadows under it, and nothing elsewhere. The
    /// pixels are 0xAARRGGBB, top row first; the result is premultiplied, as UpdateLayeredWindow takes it.
    /// </summary>
    public static void Compose(Span<uint> pixels, int width, int height, in Shape shape, uint border, ReadOnlySpan<Shadow> shadows)
    {
        // A pixel this deep inside is all card, whatever the shadow does.
        float solid = -(shape.Border + 0.5f);
        for (int y = 0; y < height; y++)
        {
            float py = y + 0.5f;
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                float px = x + 0.5f;
                float distance = Distance(px, py, shape);
                if (distance <= solid)
                {
                    pixels[index] = 0xFF000000 | (pixels[index] & 0x00FFFFFF);
                    continue;
                }

                float cover = Coverage(distance);
                float inner = Coverage(distance + shape.Border);
                float shade = 0;
                if (cover < 1)
                {
                    float clear = 1;
                    foreach (Shadow shadow in shadows)
                    {
                        float fromShadow = Distance(px, py - shadow.OffsetY, shape);
                        clear *= 1 - (shadow.Opacity * 0.5f * Erfc(fromShadow / (shadow.Sigma * Sqrt2)));
                    }

                    shade = 1 - clear;
                }

                // The shadow's faint tail is left out: a layered window lets clicks through only where it's fully clear.
                float alpha = cover + (shade * (1 - cover));
                if (alpha * 255 < 2)
                {
                    pixels[index] = 0;
                    continue;
                }

                // The shadow is black, so it adds opacity but no color.
                float edge = cover - inner;
                uint content = pixels[index];
                pixels[index] = Pack(
                    alpha,
                    (inner * ((content >> 16) & 0xFF)) + (edge * ((border >> 16) & 0xFF)),
                    (inner * ((content >> 8) & 0xFF)) + (edge * ((border >> 8) & 0xFF)),
                    (inner * (content & 0xFF)) + (edge * (border & 0xFF)));
            }
        }
    }

    /// <summary>Gets the signed distance from a point to the outline of the card and its arrow: negative inside.</summary>
    internal static float Distance(float x, float y, in Shape shape)
    {
        float halfWidth = (shape.Right - shape.Left) / 2;
        float halfHeight = (shape.Bottom - shape.Top) / 2;
        float radius = Math.Min(shape.Radius, Math.Min(halfWidth, halfHeight));
        float qx = Math.Abs(x - (shape.Left + halfWidth)) - (halfWidth - radius);
        float qy = Math.Abs(y - (shape.Top + halfHeight)) - (halfHeight - radius);
        float card = MathF.Sqrt((Math.Max(qx, 0) * Math.Max(qx, 0)) + (Math.Max(qy, 0) * Math.Max(qy, 0))) + Math.Min(Math.Max(qx, qy), 0) - radius;

        // The arrow is a triangle from its tip to the card, reaching a little into it, so no outline shows where they meet.
        // Past its tip, the nearest point is the tip itself.
        float depth = shape.ArrowUp ? y - shape.ArrowY : shape.ArrowY - y;
        float across = Math.Abs(x - shape.ArrowX);
        float slant = (across * shape.ArrowHalfWidth) + (depth * shape.ArrowHeight) < 0
            ? MathF.Sqrt((across * across) + (depth * depth))
            : ((across * shape.ArrowHeight) - (depth * shape.ArrowHalfWidth)) / MathF.Sqrt((shape.ArrowHeight * shape.ArrowHeight) + (shape.ArrowHalfWidth * shape.ArrowHalfWidth));
        float arrow = Math.Max(slant, depth - (shape.ArrowHeight + shape.Border + 2));
        return Math.Min(card, arrow);
    }

    /// <summary>Gets the complementary error function, to within 1.5e-7 (Abramowitz and Stegun, 7.1.26).</summary>
    internal static float Erfc(float x)
    {
        float z = Math.Abs(x);
        float t = 1 / (1 + (0.3275911f * z));
        float erfc = t * (0.254829592f + (t * (-0.284496736f + (t * (1.421413741f + (t * (-1.453152027f + (t * 1.061405429f)))))))) * MathF.Exp(-z * z);
        return x >= 0 ? erfc : 2 - erfc;
    }

    // How much of a pixel the shape covers, from its center's distance to the outline.
    private static float Coverage(float distance) => Math.Clamp(0.5f - distance, 0, 1);

    private static uint Pack(float alpha, float red, float green, float blue)
    {
        uint a = (uint)Math.Clamp(MathF.Round(alpha * 255), 0, 255);
        return (a << 24) | (Channel(red, a) << 16) | (Channel(green, a) << 8) | Channel(blue, a);

        // Premultiplied, a channel never exceeds alpha.
        static uint Channel(float value, uint alpha) => (uint)Math.Clamp(MathF.Round(value), 0, alpha);
    }
}

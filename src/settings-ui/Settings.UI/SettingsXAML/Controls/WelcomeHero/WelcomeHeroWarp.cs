// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Geometry of the "Warp" intro, where every tool drops out of hyperspace into its tile.
    /// <para>
    /// The stage is a perspective plane, so a point with a fixed (x, y) that moves in depth travels along a
    /// straight ray out of the vanishing point, and its distance from that point is simply divided by the
    /// perspective divisor. That lets every light streak be a flat sprite on a rotated ray whose two ends are
    /// cheap expressions of depth, instead of a 3D shape.
    /// </para>
    /// </summary>
    internal static class WelcomeHeroWarp
    {
        public const float PerspectiveDistance = 900f;

        /// <summary>
        /// Lower bound of the perspective divisor, which keeps streaks that fly past the viewer at a sane size.
        /// </summary>
        public const float MinPerspectiveDivisor = 0.3f;

        // Half the width of the logo gradient that tints the streaks, centered on the logo.
        private const float BrandSpread = 420f;

        /// <summary>
        /// Creates the perspective transform of the stage. The vanishing point sits <paramref name="funnelCenter"/>
        /// above the logo, in the middle of the funnel of tiles.
        /// </summary>
        public static Matrix4x4 CreateStageTransform(float funnelCenter) =>
            Matrix4x4.CreateTranslation(0f, funnelCenter, 0f) *
            new Matrix4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1f / PerspectiveDistance, 0, 0, 0, 1) *
            Matrix4x4.CreateTranslation(0f, -funnelCenter, 0f);

        /// <summary>
        /// Gets the vanishing point of the stage, relative to the logo center.
        /// </summary>
        public static Vector2 GetVanishingPoint(float funnelCenter) => new(0f, -funnelCenter);

        /// <summary>
        /// Gets the ray out of the vanishing point that a point at <paramref name="pointFromApex"/> travels on
        /// when it moves in depth.
        /// </summary>
        public static Ray GetRay(Vector2 pointFromApex, float funnelCenter)
        {
            var fromVanishingPoint = pointFromApex - GetVanishingPoint(funnelCenter);
            return new Ray(fromVanishingPoint.Length(), MathF.Atan2(fromVanishingPoint.Y, fromVanishingPoint.X));
        }

        /// <summary>
        /// Projects the point on <paramref name="ray"/> at depth <paramref name="z"/> (negative = away from the
        /// viewer) onto the screen, relative to the logo center.
        /// </summary>
        public static Vector2 Project(Ray ray, float z, float funnelCenter)
        {
            var distance = ray.Length / Math.Max(1f - (z / PerspectiveDistance), MinPerspectiveDivisor);
            return GetVanishingPoint(funnelCenter) + (new Vector2(MathF.Cos(ray.Angle), MathF.Sin(ray.Angle)) * distance);
        }

        /// <summary>
        /// Gets the color of the logo gradient at <paramref name="x"/> (relative to the logo center), so
        /// streaks on the left glow orange and streaks on the right glow blue.
        /// </summary>
        public static Color BrandColorAt(float x)
        {
            var colors = WelcomeHeroPalette.BrandColors;
            var position = Math.Clamp((x + BrandSpread) / (2f * BrandSpread), 0f, 1f) * (colors.Length - 1);
            var index = Math.Min((int)position, colors.Length - 2);
            var fraction = position - index;
            var from = colors[index];
            var to = colors[index + 1];
            return Color.FromArgb(
                0xFF,
                (byte)MathF.Round(from.R + ((to.R - from.R) * fraction)),
                (byte)MathF.Round(from.G + ((to.G - from.G) * fraction)),
                (byte)MathF.Round(from.B + ((to.B - from.B) * fraction)));
        }

        /// <summary>
        /// Creates the star field that rushes past the viewer while the tools warp in.
        /// </summary>
        /// <param name="count">Number of stars.</param>
        /// <param name="funnelCenter">Height of the vanishing point above the logo.</param>
        /// <param name="warpMs">Time until the last tool has landed.</param>
        public static Star[] CreateStars(int count, float funnelCenter, float warpMs)
        {
            var colors = WelcomeHeroPalette.BrandColors;
            var stars = new Star[Math.Max(count, 0)];
            for (var i = 0; i < stars.Length; i++)
            {
                // An elliptical cloud around the vanishing point: wide rather than tall, like the hero itself.
                var angle = Hash(i + 200) * MathF.Tau;
                var radius = 50f + (MathF.Pow(Hash(i + 300), 0.7f) * 700f);
                var position = new Vector2(MathF.Cos(angle) * radius, -funnelCenter + (MathF.Sin(angle) * radius * 0.6f));
                var color = Hash(i + 700) < 0.3f ? (Color?)null : colors[Math.Min((int)(Hash(i + 800) * colors.Length), colors.Length - 1)];

                stars[i] = new Star(
                    GetRay(position, funnelCenter),
                    StartZ: -(600f + (Hash(i + 400) * 5400f)),
                    DelayMs: Hash(i + 500) * 250f,
                    DurationMs: warpMs * (0.7f + (Hash(i + 600) * 0.4f)),
                    Thickness: (1f + (Hash(i + 900) * 1.8f)) * 2.2f,
                    Color: color);
            }

            return stars;
        }

        /// <summary>
        /// Deterministic 0..1 noise, so every launch has the same organic jitter.
        /// </summary>
        public static float Hash(int i)
        {
            var x = MathF.Sin((i + 1) * 12.9898f) * 43758.5453f;
            return x - MathF.Floor(x);
        }

        /// <summary>
        /// A ray out of the vanishing point.
        /// </summary>
        /// <param name="Length">Distance from the vanishing point at depth 0, in DIPs.</param>
        /// <param name="Angle">Direction, in radians (clockwise from the positive x axis).</param>
        internal readonly record struct Ray(float Length, float Angle);

        /// <summary>
        /// A star that streaks past the viewer.
        /// </summary>
        /// <param name="Ray">The ray the star travels on.</param>
        /// <param name="StartZ">Depth at launch (negative = away from the viewer).</param>
        /// <param name="DelayMs">Launch time.</param>
        /// <param name="DurationMs">Flight time.</param>
        /// <param name="Thickness">Streak thickness at depth 0, in DIPs.</param>
        /// <param name="Color">Streak color, or null for the neutral starlight of the theme.</param>
        internal readonly record struct Star(Ray Ray, float StartZ, float DelayMs, float DurationMs, float Thickness, Color? Color);
    }
}

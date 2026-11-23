// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// The pointer-driven motion of the hero stage: a dock-like magnification field, a subtle 3D tilt and the idle
    /// bob of the tiles. The scene runs these as compositor expressions; the same math runs here on the CPU, so hit
    /// testing follows what is actually on screen.
    /// </summary>
    internal static class WelcomeHeroPointerEffects
    {
        public const float MaxMagnification = 1.26f;
        public const float MagnifyBoost = MaxMagnification - 1f;
        public const float MagnifyRadius = 120f;
        public const float MaxTiltDegrees = 5f;
        public const float BobAmplitude = 5f;

        /// <summary>
        /// Scale of a tile at <paramref name="slotOffset"/> for a pointer at <paramref name="pointerFromApex"/>.
        /// Mirrors the "magnify" expression of the scene.
        /// </summary>
        public static float GetMagnification(Vector2 pointerFromApex, Vector2 slotOffset, float strength)
        {
            var distance = Math.Min((pointerFromApex - slotOffset).Length() / MagnifyRadius, 1f);
            var falloff = 1f - (distance * distance);
            return 1f + (MagnifyBoost * strength * falloff * falloff);
        }

        /// <summary>
        /// Rotation of the tilt rigs, in degrees, for a pointer at <paramref name="pointerFromApex"/>.
        /// Mirrors the "tiltX" and "tiltY" expressions of the scene.
        /// </summary>
        public static (float AroundX, float AroundY) GetTilt(Vector2 pointerFromApex, float halfWidth, float funnelCenter, float strength)
        {
            var aroundY = strength * MaxTiltDegrees * Math.Clamp(pointerFromApex.X / Math.Max(halfWidth, 1f), -1f, 1f);
            var aroundX = -strength * MaxTiltDegrees * Math.Clamp((pointerFromApex.Y + funnelCenter) / Math.Max(funnelCenter, 1f), -1f, 1f);
            return (aroundX, aroundY);
        }

        /// <summary>
        /// Creates the transform from tile space (inside both tilt rigs) to the screen, relative to the logo center:
        /// the X tilt, then the Y tilt, both around the middle of the funnel, then the perspective of the stage.
        /// </summary>
        public static Matrix4x4 CreateStageProjection(float aroundXDegrees, float aroundYDegrees, float funnelCenter)
        {
            var toPivot = Matrix4x4.CreateTranslation(0f, funnelCenter, 0f);
            var fromPivot = Matrix4x4.CreateTranslation(0f, -funnelCenter, 0f);
            var tiltX = toPivot * Matrix4x4.CreateRotationX(DegreesToRadians(aroundXDegrees)) * fromPivot;
            var tiltY = toPivot * Matrix4x4.CreateRotationY(DegreesToRadians(aroundYDegrees)) * fromPivot;
            return tiltX * tiltY * WelcomeHeroWarp.CreateStageTransform(funnelCenter);
        }

        /// <summary>
        /// Projects a point of the flat tile plane through <paramref name="projection"/>, onto the screen.
        /// </summary>
        public static Vector2 Project(Vector2 point, Matrix4x4 projection)
        {
            var projected = Vector4.Transform(new Vector4(point, 0f, 1f), projection);
            var w = Math.Max(projected.W, WelcomeHeroWarp.MinPerspectiveDivisor);
            return new Vector2(projected.X / w, projected.Y / w);
        }

        private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);
    }

    /// <summary>
    /// The motion that is currently applied to the hero stage, as far as hit testing is concerned.
    /// </summary>
    /// <param name="PointerStrength">How strongly the pointer effects are applied (0 = off, 1 = pointer over the hero).</param>
    /// <param name="HalfWidth">Half the width of the hero, which the tilt is relative to.</param>
    /// <param name="BobAmplitude">How far the tiles bob upwards while idle (0 = no bob).</param>
    internal readonly record struct WelcomeHeroMotion(float PointerStrength, float HalfWidth, float BobAmplitude)
    {
        /// <summary>
        /// Gets the motion of a static hero: every tile sits exactly on its slot.
        /// </summary>
        public static WelcomeHeroMotion None => default;

        /// <summary>
        /// Gets the motion of a live hero with the pointer over it.
        /// </summary>
        public static WelcomeHeroMotion Live(float heroWidth) => new(1f, Math.Max(heroWidth / 2f, 1f), WelcomeHeroPointerEffects.BobAmplitude);
    }
}

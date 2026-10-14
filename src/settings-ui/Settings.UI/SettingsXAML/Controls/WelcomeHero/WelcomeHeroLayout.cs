// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Geometry of the Welcome hero: a diamond lattice of glass tiles that fans out of the
    /// PowerToys logo in a V-shaped funnel. Every position is relative to the logo center
    /// (the apex), so resizing the hero only moves the apex.
    /// </summary>
    public sealed class WelcomeHeroLayout
    {
        public const float DefaultHeroHeight = 320f;
        public const float TileSize = 52f;
        public const float TileCornerRadius = 12f;
        public const float IconSize = 30f;
        public const float LogoSize = 76f;
        public const float Spacing = 68f;
        public const float ApexBottomInset = 62f;
        public const float VisibleOpacityThreshold = 0.5f;

        private const int MaxColumns = 16;
        private const float HitTolerance = 4f;

        public WelcomeHeroLayout(float heroHeight)
        {
            HeroHeight = Math.Max(heroHeight, LogoSize + ApexBottomInset);
            ApexY = HeroHeight - ApexBottomInset;
            Slots = BuildSlots(ApexY);
            MaxSlotDistance = Slots.Count > 0 ? Slots.Max(s => s.Distance) : 0f;
        }

        public float HeroHeight { get; }

        /// <summary>
        /// Gets the vertical position of the logo center, measured from the top of the hero.
        /// </summary>
        public float ApexY { get; }

        /// <summary>
        /// Gets the slots in the order they are handed out to icons: clearly visible slots
        /// first (closest to the logo first), followed by the faded slots at the top edge.
        /// </summary>
        public IReadOnlyList<WelcomeHeroSlot> Slots { get; }

        public float MaxSlotDistance { get; }

        /// <summary>
        /// Gets the height of the middle of the funnel above the logo, which the stage tilts around and the
        /// perspective vanishes into.
        /// </summary>
        public float FunnelCenter => ApexY / 2f;

        public Vector2 GetApex(float heroWidth) => new(heroWidth / 2f, ApexY);

        /// <summary>
        /// Returns the index of the slot under <paramref name="pointFromApex"/>, or -1.
        /// </summary>
        public int HitTestSlot(Vector2 pointFromApex, int slotCount) => HitTestSlot(pointFromApex, slotCount, WelcomeHeroMotion.None);

        /// <summary>
        /// Returns the index of the slot whose tile is drawn under <paramref name="pointFromApex"/>, or -1, taking the
        /// magnification, tilt and bob of <paramref name="motion"/> into account.
        /// </summary>
        internal int HitTestSlot(Vector2 pointFromApex, int slotCount, WelcomeHeroMotion motion)
        {
            var projection = CreateProjection(pointFromApex, motion);
            var half = (TileSize / 2f) + HitTolerance;
            var count = Math.Min(slotCount, Slots.Count);
            var hit = -1;
            var hitDistance = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var slot = Slots[i];
                if (slot.Opacity < 0.35f)
                {
                    continue;
                }

                var scale = WelcomeHeroPointerEffects.GetMagnification(pointFromApex, slot.Offset, motion.PointerStrength);
                if (!HitTestSquare(pointFromApex, slot.Offset, half * scale, -motion.BobAmplitude * scale, projection))
                {
                    continue;
                }

                // Magnified neighbors can touch: the tile closest to the pointer wins.
                var distance = Vector2.DistanceSquared(pointFromApex, WelcomeHeroPointerEffects.Project(slot.Offset, projection));
                if (distance < hitDistance)
                {
                    hit = i;
                    hitDistance = distance;
                }
            }

            return hit;
        }

        public static bool HitTestLogo(Vector2 pointFromApex)
        {
            var half = LogoSize / 2f;
            return Math.Abs(pointFromApex.X) <= half && Math.Abs(pointFromApex.Y) <= half;
        }

        /// <summary>
        /// Returns whether the logo is drawn under <paramref name="pointFromApex"/>, taking the tilt of
        /// <paramref name="motion"/> into account.
        /// </summary>
        internal bool HitTestLogo(Vector2 pointFromApex, WelcomeHeroMotion motion) =>
            HitTestSquare(pointFromApex, Vector2.Zero, LogoSize / 2f, 0f, CreateProjection(pointFromApex, motion));

        /// <summary>
        /// Gets where the center of the slot at <paramref name="index"/> is drawn, relative to the logo center.
        /// </summary>
        internal Vector2 GetSlotCenter(int index, Vector2 pointerFromApex, WelcomeHeroMotion motion) =>
            WelcomeHeroPointerEffects.Project(Slots[index].Offset, CreateProjection(pointerFromApex, motion));

        private static bool HitTestSquare(Vector2 point, Vector2 center, float half, float bobOffset, Matrix4x4 projection)
        {
            // The tile bobs up and down while idle; accept the whole range it travels.
            var top = center.Y - half + Math.Min(bobOffset, 0f);
            var bottom = center.Y + half;
            return IsInsideConvexQuad(
                point,
                WelcomeHeroPointerEffects.Project(new Vector2(center.X - half, top), projection),
                WelcomeHeroPointerEffects.Project(new Vector2(center.X + half, top), projection),
                WelcomeHeroPointerEffects.Project(new Vector2(center.X + half, bottom), projection),
                WelcomeHeroPointerEffects.Project(new Vector2(center.X - half, bottom), projection));
        }

        private static bool IsInsideConvexQuad(Vector2 point, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            static float Cross(Vector2 from, Vector2 to, Vector2 p) => ((to.X - from.X) * (p.Y - from.Y)) - ((to.Y - from.Y) * (p.X - from.X));

            var ab = Cross(a, b, point);
            var bc = Cross(b, c, point);
            var cd = Cross(c, d, point);
            var da = Cross(d, a, point);
            return (ab >= 0f && bc >= 0f && cd >= 0f && da >= 0f) || (ab <= 0f && bc <= 0f && cd <= 0f && da <= 0f);
        }

        private Matrix4x4 CreateProjection(Vector2 pointerFromApex, WelcomeHeroMotion motion)
        {
            var (aroundX, aroundY) = WelcomeHeroPointerEffects.GetTilt(pointerFromApex, motion.HalfWidth, FunnelCenter, motion.PointerStrength);
            return WelcomeHeroPointerEffects.CreateStageProjection(aroundX, aroundY, FunnelCenter);
        }

        /// <summary>
        /// Opacity of a tile whose center sits at <paramref name="y"/> (from the top of the hero).
        /// Tiles dissolve as they approach the top edge, which makes the funnel feel endless.
        /// </summary>
        public static float EdgeOpacity(float y)
        {
            var t = Math.Clamp((y + (TileSize * 0.45f)) / (TileSize * 1.2f), 0f, 1f);
            return t * t * (3f - (2f * t));
        }

        private static List<WelcomeHeroSlot> BuildSlots(float apexY)
        {
            var candidates = new List<(int Column, int Row, Vector2 Offset, float Distance, float Opacity)>();

            for (var column = -MaxColumns; column <= MaxColumns; column++)
            {
                var halfStep = Math.Abs(column) / 2f;

                // The logo occupies the bottom of the center column.
                for (var row = column == 0 ? 1 : 0; ; row++)
                {
                    var offset = new Vector2(column * Spacing, -Spacing * (row + halfStep));
                    var opacity = EdgeOpacity(apexY + offset.Y);
                    if (opacity <= 0.05f)
                    {
                        break;
                    }

                    candidates.Add((column, row, offset, offset.Length(), opacity));
                }
            }

            // Clearly visible slots come first so every module icon gets a good spot; the faded
            // slots at the top edge are handed out last and usually end up as empty ghost tiles.
            return candidates
                .OrderBy(c => c.Opacity < VisibleOpacityThreshold ? 1 : 0)
                .ThenBy(c => MathF.Round(c.Distance, 2))
                .ThenBy(c => Math.Abs(c.Column))
                .ThenBy(c => c.Column)
                .Select((c, rank) => new WelcomeHeroSlot(rank, c.Column, c.Row, c.Offset, c.Distance, c.Opacity))
                .ToList();
        }
    }
}

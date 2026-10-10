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

        public Vector2 GetApex(float heroWidth) => new(heroWidth / 2f, ApexY);

        /// <summary>
        /// Returns the index of the slot under <paramref name="pointFromApex"/>, or -1.
        /// </summary>
        public int HitTestSlot(Vector2 pointFromApex, int slotCount)
        {
            var half = (TileSize / 2f) + HitTolerance;
            var count = Math.Min(slotCount, Slots.Count);
            for (var i = 0; i < count; i++)
            {
                var slot = Slots[i];
                if (slot.Opacity < 0.35f)
                {
                    continue;
                }

                var delta = pointFromApex - slot.Offset;
                if (Math.Abs(delta.X) <= half && Math.Abs(delta.Y) <= half)
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool HitTestLogo(Vector2 pointFromApex)
        {
            var half = LogoSize / 2f;
            return Math.Abs(pointFromApex.X) <= half && Math.Abs(pointFromApex.Y) <= half;
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

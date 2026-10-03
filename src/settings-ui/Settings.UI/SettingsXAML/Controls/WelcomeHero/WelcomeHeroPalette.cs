// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Theme-dependent colors of the Welcome hero.
    /// </summary>
    internal sealed record WelcomeHeroPalette(
        Color Dome,
        Color TileFillTop,
        Color TileFillBottom,
        Color TileStroke,
        Color GhostFill,
        Color GhostStroke,
        Color Aura,
        Color Flash,
        Color Starlight,
        float GlowAlpha,
        float AuroraRestOpacity,
        Color[] Highlight,
        bool ShowEffects)
    {
        // Colors sampled from the PowerToys logo, left to right.
        public static readonly Color BrandOrange = Color.FromArgb(0xFF, 0xE3, 0x52, 0x14);
        public static readonly Color BrandYellow = Color.FromArgb(0xFF, 0xFE, 0xD5, 0x00);
        public static readonly Color BrandGreen = Color.FromArgb(0xFF, 0x5E, 0xD1, 0x3F);
        public static readonly Color BrandBlue = Color.FromArgb(0xFF, 0x14, 0x94, 0xDF);

        public static Color[] BrandColors => [BrandOrange, BrandYellow, BrandGreen, BrandBlue];

        public static WelcomeHeroPalette Dark { get; } = new(
            Dome: Color.FromArgb(0xFF, 0x06, 0x15, 0x2E),
            TileFillTop: Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF),
            TileFillBottom: Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF),
            TileStroke: Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF),
            GhostFill: Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF),
            GhostStroke: Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF),
            Aura: Color.FromArgb(0x70, 0xE9, 0xDE, 0xC3),
            Flash: Color.FromArgb(0xFF, 0xFF, 0xF6, 0xE0),
            Starlight: Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
            GlowAlpha: 0.55f,
            AuroraRestOpacity: 0.4f,
            Highlight: BrandColors,
            ShowEffects: true);

        public static WelcomeHeroPalette Light { get; } = new(
            Dome: Color.FromArgb(0xD8, 0x91, 0xB5, 0xD0),
            TileFillTop: Color.FromArgb(0xD6, 0xFF, 0xFF, 0xFF),
            TileFillBottom: Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF),
            TileStroke: Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF),
            GhostFill: Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF),
            GhostStroke: Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF),
            Aura: Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
            Flash: Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
            Starlight: Color.FromArgb(0xFF, 0x74, 0x83, 0x9A),
            GlowAlpha: 0.45f,
            AuroraRestOpacity: 0.14f,
            Highlight: BrandColors,
            ShowEffects: true);

        public static WelcomeHeroPalette HighContrast(Color text, Color highlight) => new(
            Dome: Color.FromArgb(0, 0, 0, 0),
            TileFillTop: Color.FromArgb(0, 0, 0, 0),
            TileFillBottom: Color.FromArgb(0, 0, 0, 0),
            TileStroke: text,
            GhostFill: Color.FromArgb(0, 0, 0, 0),
            GhostStroke: Color.FromArgb(0, 0, 0, 0),
            Aura: Color.FromArgb(0, 0, 0, 0),
            Flash: Color.FromArgb(0, 0, 0, 0),
            Starlight: Color.FromArgb(0, 0, 0, 0),
            GlowAlpha: 0f,
            AuroraRestOpacity: 0f,
            Highlight: [highlight, highlight, highlight, highlight],
            ShowEffects: false);
    }
}

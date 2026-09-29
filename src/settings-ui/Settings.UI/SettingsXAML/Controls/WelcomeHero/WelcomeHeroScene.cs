// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// The Composition visual tree behind the Welcome hero ("Power on").
    /// <para>
    /// The PowerToys logo ignites, a dome of light rises behind it, every tool erupts out of the logo
    /// and lands in its glass tile, and a shockwave in the logo colors ripples through the constellation.
    /// Afterwards the scene stays alive: tiles float, the aurora drifts, and the pointer tilts the scene and
    /// magnifies nearby tiles.
    /// </para>
    /// <para>
    /// Every animated property lives on its own visual level (slot = magnification, floater = idle bob and
    /// shockwave pulse, tile = intro flight), so the whole timeline starts in a single frame using delays.
    /// </para>
    /// </summary>
    internal sealed partial class WelcomeHeroScene : IDisposable
    {
        /// <summary>
        /// Scale of a tile right under the pointer.
        /// </summary>
        internal const float MaxMagnification = 1.26f;

        // Intro timeline, in milliseconds.
        private const float LogoIntroMs = 700f;
        private const float FlashDelayMs = 60f;
        private const float FlashMs = 620f;
        private const float DomeDelayMs = 90f;
        private const float DomeMs = 1100f;
        private const float GlowDelayMs = 140f;
        private const float GlowMs = 1500f;
        private const float EruptionStartMs = 300f;
        private const float EruptionMsPerDip = 1.45f;
        private const float FlightMs = 820f;
        private const float ShockwaveStartMs = 1750f;
        private const float RingMs = 1100f;
        private const float TilePulseMs = 560f;
        private const float ShineIntervalMs = 7000f;
        private const float IdleStartMs = 2700f;

        // Settle timeline (repeat visits), in milliseconds.
        private const float SettleMs = 450f;
        private const float SettleStaggerMs = 7f;

        private const float FlightDepth = -380f;
        private const float LogoDepth = -300f;
        private const float PerspectiveDistance = 900f;
        private const float MagnifyBoost = MaxMagnification - 1f;
        private const float MagnifyRadius = 120f;
        private const float MaxTiltDegrees = 5f;
        private const float BobAmplitude = 5f;

        private const float CornerRadius = 7f;
        private const float LightSpill = 480f;
        private const float DomeWidth = 2000f;
        private static readonly Vector2 DomeCenter = new(DomeWidth / 2f, -90f);
        private static readonly Vector2 DomeRadius = new(1000f, 420f);
        private static readonly Vector2 GlowSize = new(620f, 400f);
        private static readonly float[] FlashAlphas = [1f, 0.55f, 0.18f, 0f];
        private static readonly Vector2[] GlowPositions =
        [
            new(-340f, -120f),
            new(-115f, -175f),
            new(115f, -175f),
            new(340f, -120f),
        ];

        private readonly Compositor _compositor;
        private readonly WelcomeHeroLayout _layout;
        private readonly List<IDisposable> _resources = [];

        private readonly CompositionEasingFunction _linear;
        private readonly CompositionEasingFunction _easeOutExpo;
        private readonly CompositionEasingFunction _easeOutCubic;
        private readonly CompositionEasingFunction _easeInOutSine;
        private readonly CompositionEasingFunction _easeInQuad;

        private readonly RectangleClip _clip;
        private readonly ContainerVisual _auroraRig;
        private readonly ContainerVisual _auroraParallax;
        private readonly SpriteVisual _dome;
        private readonly CompositionRadialGradientBrush _domeBrush;
        private readonly CompositionColorGradientStop[] _domeStops;
        private readonly float[] _domeStopAlphas;
        private readonly Glow[] _glows;
        private readonly SpriteVisual _flash;
        private readonly CompositionColorGradientStop[] _flashStops;

        private readonly ContainerVisual _stage;
        private readonly ContainerVisual _tiltY;
        private readonly ContainerVisual _tiltX;
        private readonly ShapeVisual _ring;
        private readonly CompositionEllipseGeometry _ringGeometry;
        private readonly CompositionSpriteShape _ringShape;

        private readonly Tile[] _tiles;
        private readonly CompositionColorGradientStop _tileFillTop;
        private readonly CompositionColorGradientStop _tileFillBottom;
        private readonly CompositionColorBrush _tileStroke;
        private readonly CompositionColorBrush _ghostFill;
        private readonly CompositionColorBrush _ghostStroke;
        private readonly CompositionColorGradientStop[] _highlightStops;

        private readonly ContainerVisual _logoRig;
        private readonly ContainerVisual _logoPulse;
        private readonly SpriteVisual _aura;
        private readonly CompositionColorGradientStop[] _auraStops;
        private readonly CompositionLinearGradientBrush _shineBrush;

        private readonly CompositionPropertySet _interaction;

        private WelcomeHeroPalette _palette;
        private int _highlighted = -1;
        private bool _hasViewport;
        private bool _entered;
        private bool _disposed;

        public WelcomeHeroScene(
            Compositor compositor,
            WelcomeHeroLayout layout,
            IReadOnlyList<LoadedImageSurface> icons,
            LoadedImageSurface logo,
            WelcomeHeroPalette palette)
        {
            _compositor = compositor;
            _layout = layout;
            _palette = palette;

            _linear = Track(compositor.CreateLinearEasingFunction());
            _easeOutExpo = Track(compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f)));
            _easeOutCubic = Track(compositor.CreateCubicBezierEasingFunction(new Vector2(0.33f, 1f), new Vector2(0.68f, 1f)));
            _easeInOutSine = Track(compositor.CreateCubicBezierEasingFunction(new Vector2(0.37f, 0f), new Vector2(0.63f, 1f)));
            _easeInQuad = Track(compositor.CreateCubicBezierEasingFunction(new Vector2(0.11f, 0f), new Vector2(0.5f, 0f)));

            _interaction = Track(compositor.CreatePropertySet());
            _interaction.InsertScalar("Strength", 0f);
            _interaction.InsertScalar("Boost", MagnifyBoost);
            _interaction.InsertScalar("Radius", MagnifyRadius);
            _interaction.InsertScalar("MaxTilt", MaxTiltDegrees);
            _interaction.InsertScalar("HalfWidth", 500f);
            _interaction.InsertScalar("HalfHeight", layout.ApexY / 2f);
            _interaction.InsertScalar("CenterY", layout.ApexY / 2f);

            Root = Track(compositor.CreateContainerVisual());

            // Match the rounded top corners of the page card. Light may spill below the hero, onto the page.
            _clip = Track(compositor.CreateRectangleClip());
            _clip.TopLeftRadius = new Vector2(CornerRadius);
            _clip.TopRightRadius = new Vector2(CornerRadius);
            Root.Clip = _clip;

            // --- Aurora: the dome of light and the brand colored glows behind everything. ---
            _auroraRig = Track(compositor.CreateContainerVisual());
            _auroraParallax = Track(compositor.CreateContainerVisual());
            _auroraRig.Children.InsertAtTop(_auroraParallax);
            Root.Children.InsertAtTop(_auroraRig);

            _domeStopAlphas = [1f, 1f, 0.82f, 0.45f, 0.14f, 0f];
            var domeStopOffsets = new[] { 0f, 0.5f, 0.66f, 0.8f, 0.92f, 1f };
            _domeBrush = Track(compositor.CreateRadialGradientBrush());
            _domeBrush.MappingMode = CompositionMappingMode.Absolute;
            _domeBrush.EllipseCenter = DomeCenter;
            _domeBrush.EllipseRadius = DomeRadius;
            _domeStops = new CompositionColorGradientStop[domeStopOffsets.Length];
            for (var i = 0; i < _domeStops.Length; i++)
            {
                _domeStops[i] = Track(compositor.CreateColorGradientStop(domeStopOffsets[i], WithAlpha(palette.Dome, _domeStopAlphas[i])));
                _domeBrush.ColorStops.Add(_domeStops[i]);
            }

            _dome = Track(compositor.CreateSpriteVisual());
            _dome.Brush = _domeBrush;
            _dome.Size = new Vector2(DomeWidth, layout.HeroHeight + LightSpill);
            _dome.Offset = new Vector3(-DomeWidth / 2f, 0f, 0f);
            _auroraParallax.Children.InsertAtTop(_dome);

            var brandColors = WelcomeHeroPalette.BrandColors;
            _glows = new Glow[GlowPositions.Length];
            for (var i = 0; i < _glows.Length; i++)
            {
                _glows[i] = CreateGlow(brandColors[i], palette.GlowAlpha, GlowPositions[i]);
                _auroraParallax.Children.InsertAtTop(_glows[i].Drift);
            }

            (_flash, _flashStops) = CreateRadialSprite(new Vector2(320f, 320f), palette.Flash, FlashAlphas, [0f, 0.25f, 0.6f, 1f]);
            _flash.Offset = new Vector3(-160f, layout.ApexY - 160f, 0f);
            _flash.CenterPoint = new Vector3(160f, 160f, 0f);
            _auroraParallax.Children.InsertAtTop(_flash);

            // --- Stage: the tilting 3D plane that holds the ring, the tiles and the logo. ---
            _stage = Track(compositor.CreateContainerVisual());
            var funnelCenter = layout.ApexY / 2f;
            _stage.TransformMatrix =
                Matrix4x4.CreateTranslation(0f, funnelCenter, 0f) *
                new Matrix4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1f / PerspectiveDistance, 0, 0, 0, 1) *
                Matrix4x4.CreateTranslation(0f, -funnelCenter, 0f);
            Root.Children.InsertAtTop(_stage);

            _tiltY = Track(compositor.CreateContainerVisual());
            _tiltY.RotationAxis = Vector3.UnitY;
            _tiltY.CenterPoint = new Vector3(0f, -funnelCenter, 0f);
            _stage.Children.InsertAtTop(_tiltY);

            _tiltX = Track(compositor.CreateContainerVisual());
            _tiltX.RotationAxis = Vector3.UnitX;
            _tiltX.CenterPoint = new Vector3(0f, -funnelCenter, 0f);
            _tiltY.Children.InsertAtTop(_tiltX);

            // Shockwave ring, drawn behind the tiles.
            var ringExtent = (layout.MaxSlotDistance + 160f) * 2f;
            var ringBrush = Track(compositor.CreateLinearGradientBrush());
            ringBrush.MappingMode = CompositionMappingMode.Absolute;
            ringBrush.StartPoint = new Vector2((ringExtent / 2f) - 420f, 0f);
            ringBrush.EndPoint = new Vector2((ringExtent / 2f) + 420f, 0f);
            for (var i = 0; i < brandColors.Length; i++)
            {
                ringBrush.ColorStops.Add(Track(compositor.CreateColorGradientStop(i / (float)(brandColors.Length - 1), brandColors[i])));
            }

            _ringGeometry = Track(compositor.CreateEllipseGeometry());
            _ringGeometry.Center = new Vector2(ringExtent / 2f, ringExtent / 2f);
            _ringGeometry.Radius = new Vector2(WelcomeHeroLayout.LogoSize / 2f);
            _ringShape = Track(compositor.CreateSpriteShape(_ringGeometry));
            _ringShape.StrokeBrush = ringBrush;
            _ringShape.StrokeThickness = 0f;
            _ring = Track(compositor.CreateShapeVisual());
            _ring.Size = new Vector2(ringExtent);
            _ring.Offset = new Vector3(-ringExtent / 2f, -ringExtent / 2f, 0f);
            _ring.Opacity = 0f;
            _ring.Clip = Track(compositor.CreateInsetClip(0f, 0f, 0f, (ringExtent / 2f) - (layout.HeroHeight - layout.ApexY)));
            _ring.Shapes.Add(_ringShape);
            _tiltX.Children.InsertAtTop(_ring);

            // Shared tile brushes, so a theme change is only a handful of color animations.
            var tileFill = Track(compositor.CreateLinearGradientBrush());
            tileFill.StartPoint = new Vector2(0f, 0f);
            tileFill.EndPoint = new Vector2(0f, 1f);
            _tileFillTop = Track(compositor.CreateColorGradientStop(0f, palette.TileFillTop));
            _tileFillBottom = Track(compositor.CreateColorGradientStop(1f, palette.TileFillBottom));
            tileFill.ColorStops.Add(_tileFillTop);
            tileFill.ColorStops.Add(_tileFillBottom);
            _tileStroke = Track(compositor.CreateColorBrush(palette.TileStroke));
            _ghostFill = Track(compositor.CreateColorBrush(palette.GhostFill));
            _ghostStroke = Track(compositor.CreateColorBrush(palette.GhostStroke));

            const float highlightExtent = WelcomeHeroLayout.TileSize + 6f;
            var highlightBrush = Track(compositor.CreateLinearGradientBrush());
            highlightBrush.MappingMode = CompositionMappingMode.Absolute;
            highlightBrush.StartPoint = new Vector2(0f, highlightExtent);
            highlightBrush.EndPoint = new Vector2(highlightExtent, 0f);
            _highlightStops = new CompositionColorGradientStop[palette.Highlight.Length];
            for (var i = 0; i < _highlightStops.Length; i++)
            {
                _highlightStops[i] = Track(compositor.CreateColorGradientStop(i / (float)(_highlightStops.Length - 1), palette.Highlight[i]));
                highlightBrush.ColorStops.Add(_highlightStops[i]);
            }

            var slotCount = Math.Max(icons.Count, layout.Slots.Count);
            _tiles = new Tile[Math.Min(slotCount, layout.Slots.Count)];
            for (var i = 0; i < _tiles.Length; i++)
            {
                var icon = i < icons.Count ? icons[i] : null;
                _tiles[i] = CreateTile(layout.Slots[i], icon, tileFill, highlightBrush, highlightExtent);
                _tiltX.Children.InsertAtTop(_tiles[i].Slot);
            }

            // The logo sits on top of the tiles, so the tools appear to pour out of it.
            var logoSlot = Track(compositor.CreateContainerVisual());
            _tiltX.Children.InsertAtTop(logoSlot);

            _logoRig = Track(compositor.CreateContainerVisual());
            logoSlot.Children.InsertAtTop(_logoRig);

            (_aura, _auraStops) = CreateRadialSprite(new Vector2(190f, 190f), palette.Aura, [1f, 0.4f, 0f], [0f, 0.45f, 1f]);
            _aura.Offset = new Vector3(-95f, -95f, 0f);
            _aura.CenterPoint = new Vector3(95f, 95f, 0f);
            _logoRig.Children.InsertAtTop(_aura);

            _logoPulse = Track(compositor.CreateContainerVisual());
            _logoRig.Children.InsertAtTop(_logoPulse);

            const float logoSize = WelcomeHeroLayout.LogoSize;
            var logoBrush = CreateSurfaceBrush(logo);
            var logoVisual = Track(compositor.CreateSpriteVisual());
            logoVisual.Brush = logoBrush;
            logoVisual.Size = new Vector2(logoSize);
            logoVisual.Offset = new Vector3(-logoSize / 2f, -logoSize / 2f, 0f);
            _logoPulse.Children.InsertAtTop(logoVisual);

            // Specular shine: a moving gradient masked by the logo's own alpha.
            _shineBrush = Track(compositor.CreateLinearGradientBrush());
            _shineBrush.MappingMode = CompositionMappingMode.Absolute;
            _shineBrush.StartPoint = new Vector2(0f, 0f);
            _shineBrush.EndPoint = new Vector2(logoSize, logoSize);
            _shineBrush.ColorStops.Add(Track(compositor.CreateColorGradientStop(0.3f, Color.FromArgb(0, 255, 255, 255))));
            _shineBrush.ColorStops.Add(Track(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0xB0, 255, 255, 255))));
            _shineBrush.ColorStops.Add(Track(compositor.CreateColorGradientStop(0.7f, Color.FromArgb(0, 255, 255, 255))));
            _shineBrush.Offset = new Vector2(-logoSize * 1.2f, 0f);
            var shineMask = Track(compositor.CreateMaskBrush());
            shineMask.Source = _shineBrush;
            shineMask.Mask = CreateSurfaceBrush(logo);
            var shine = Track(compositor.CreateSpriteVisual());
            shine.Brush = shineMask;
            shine.Size = new Vector2(logoSize);
            shine.Offset = logoVisual.Offset;
            _logoPulse.Children.InsertAtTop(shine);

            ApplyEffectsVisibility();
        }

        public ContainerVisual Root { get; }

        public Vector2 Apex { get; private set; }

        public int TileCount => _tiles.Length;

        /// <summary>
        /// Positions the scene for a hero of the given size. Later calls glide the constellation to the new center.
        /// </summary>
        public void SetViewport(Vector2 size, float rasterizationScale)
        {
            if (size.X <= 0f || size.Y <= 0f)
            {
                return;
            }

            Root.Size = size;
            _clip.Right = size.X;
            _clip.Bottom = size.Y + LightSpill;

            // Keep the constellation on whole physical pixels so icons stay crisp at rest.
            var scale = Math.Max(rasterizationScale, 1f);
            var apexX = MathF.Round(size.X / 2f * scale) / scale;
            Apex = new Vector2(apexX, _layout.ApexY);
            _interaction.InsertScalar("HalfWidth", Math.Max(size.X / 2f, 1f));

            if (!_hasViewport)
            {
                _hasViewport = true;
                _stage.Offset = new Vector3(Apex, 0f);
                _auroraRig.Offset = new Vector3(apexX, 0f, 0f);

                var glide = Track(_compositor.CreateVector3KeyFrameAnimation());
                glide.Target = "Offset";
                glide.InsertExpressionKeyFrame(1f, "this.FinalValue", _easeOutExpo);
                glide.Duration = TimeSpan.FromMilliseconds(450);
                var implicitAnimations = Track(_compositor.CreateImplicitAnimationCollection());
                implicitAnimations["Offset"] = glide;
                _stage.ImplicitAnimations = implicitAnimations;
                _auroraRig.ImplicitAnimations = implicitAnimations;
                return;
            }

            _stage.Offset = new Vector3(Apex, 0f);
            _auroraRig.Offset = new Vector3(apexX, 0f, 0f);
        }

        /// <summary>
        /// Hides everything, so nothing flashes on screen while the icons are loading.
        /// </summary>
        public void HideAll()
        {
            _logoRig.Opacity = 0f;
            _dome.Opacity = 0f;
            _flash.Opacity = 0f;
            _ring.Opacity = 0f;
            foreach (var glow in _glows)
            {
                glow.Sprite.Opacity = 0f;
            }

            foreach (var tile in _tiles)
            {
                tile.Body.Opacity = 0f;
            }
        }

        /// <summary>
        /// Plays the full "Power on" intro. Everything is scheduled in a single frame.
        /// </summary>
        public void PlayIntro()
        {
            _entered = true;

            // 1. Ignition: the logo springs out of the depth.
            Animate(_logoRig, "Opacity", Scalar(260f, 0f, (0f, 0f, null), (1f, 1f, null)));
            Animate(_logoRig, "Scale", Vector(LogoIntroMs, 0f, (0f, Uniform(0.3f), null), (0.62f, Uniform(1.1f), _easeOutCubic), (1f, Uniform(1f), _easeInOutSine)));
            Animate(_logoRig, "Offset", Vector(LogoIntroMs, 0f, (0f, new Vector3(0f, 0f, LogoDepth), null), (1f, Vector3.Zero, _easeOutExpo)));

            // 2. A white-hot core flash and the four logo colors bursting out as light.
            Animate(_flash, "Opacity", Scalar(FlashMs, FlashDelayMs, (0f, 0f, null), (0.16f, 1f, _easeOutCubic), (1f, 0f, _easeInQuad)));
            Animate(_flash, "Scale", Vector(FlashMs, FlashDelayMs, (0f, Uniform(0.2f), null), (1f, Uniform(2.2f), _easeOutExpo)));

            for (var i = 0; i < _glows.Length; i++)
            {
                var glow = _glows[i];
                var delay = GlowDelayMs + (Math.Abs(i - 1.5f) * 50f);
                Animate(glow.Sprite, "Opacity", Scalar(GlowMs, delay, (0f, 0f, null), (0.22f, 1f, _easeOutCubic), (1f, _palette.AuroraRestOpacity, _easeInOutSine)));
                Animate(glow.Sprite, "Scale", Vector(GlowMs, delay, (0f, Uniform(0.15f), null), (1f, Vector3.One, _easeOutExpo)));
                Animate(glow.Sprite, "Offset", Vector(GlowMs, delay, (0f, new Vector3(glow.Origin, 0f), null), (1f, new Vector3(glow.Home, 0f), _easeOutExpo)));
            }

            // 3. Dawn: the dome of light grows out of the logo like a sunrise.
            var dawnCenter = new Vector2(DomeCenter.X, _layout.ApexY);
            Animate(_dome, "Opacity", Scalar(250f, DomeDelayMs, (0f, 0f, null), (1f, 1f, null)));
            Animate(_domeBrush, "EllipseCenter", Vector2D(DomeMs, DomeDelayMs, (0f, dawnCenter, null), (1f, DomeCenter, _easeOutExpo)));
            Animate(_domeBrush, "EllipseRadius", Vector2D(DomeMs, DomeDelayMs, (0f, new Vector2(90f, 60f), null), (1f, DomeRadius, _easeOutExpo)));

            // 4. Eruption: every tool is born from the logo and arcs into its tile.
            var nearest = _layout.Slots.Count > 0 ? MinDistance() : 0f;
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                var slot = tile.Layout;
                if (tile.IsGhost)
                {
                    // Empty tiles quietly materialize around the eruption.
                    var ghostDelay = EruptionStartMs + 450f + ((slot.Distance - nearest) * EruptionMsPerDip * 0.8f);
                    tile.Body.Properties.InsertScalar("P", 1f);
                    Animate(tile.Body, "Opacity", Scalar(700f, ghostDelay, (0f, 0f, null), (1f, slot.Opacity, _easeOutCubic)));
                    Animate(tile.Body, "Scale", Vector(700f, ghostDelay, (0f, Uniform(0.6f), null), (1f, Vector3.One, _easeOutExpo)));
                    continue;
                }

                var launch = EruptionStartMs + ((slot.Distance - nearest) * EruptionMsPerDip) + (Hash(i) * 50f);
                Animate(tile.Body.Properties, "P", Scalar(FlightMs, launch, (0f, 0f, null), (1f, 1f, _easeOutExpo)));
                Animate(tile.Body, "Opacity", Scalar(FlightMs, launch, (0f, 0f, null), (0.1f, slot.Opacity, null), (1f, slot.Opacity, null)));
                Animate(tile.Body, "Scale", Vector(FlightMs, launch, (0f, Uniform(0.15f), null), (0.55f, Uniform(1.12f), _easeOutCubic), (1f, Vector3.One, _easeInOutSine)));
            }

            // 5. Shockwave: the logo pulses and a ring in the logo colors ripples through every tile.
            var ringStart = WelcomeHeroLayout.LogoSize / 2f;
            var ringEnd = _layout.MaxSlotDistance + 120f;
            var radius = CreateAnimation<Vector2KeyFrameAnimation>(RingMs, ShockwaveStartMs);
            for (var k = 0; k <= 12; k++)
            {
                var t = k / 12f;
                radius.InsertKeyFrame(t, new Vector2(ringStart + ((ringEnd - ringStart) * EaseOutCubic(t))), _linear);
            }

            Animate(_ringGeometry, "Radius", radius);
            Animate(_ringShape, "StrokeThickness", Scalar(RingMs, ShockwaveStartMs, (0f, 12f, null), (1f, 1f, _easeOutCubic)));
            Animate(_ring, "Opacity", Scalar(RingMs, ShockwaveStartMs, (0f, 0f, null), (0.04f, 0.85f, null), (0.55f, 0.45f, _linear), (1f, 0f, _easeInOutSine)));

            Animate(_logoPulse, "Scale", Vector(520f, ShockwaveStartMs - 120f, (0f, Vector3.One, null), (0.3f, Uniform(1.14f), _easeOutCubic), (1f, Vector3.One, _easeInOutSine)));

            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                var reach = Math.Clamp((tile.Layout.Distance - ringStart) / (ringEnd - ringStart), 0f, 1f);
                var arrival = ShockwaveStartMs + (RingMs * (1f - MathF.Cbrt(1f - reach)));
                var strength = tile.IsGhost ? 1.08f : 1.16f;
                Animate(tile.Floater, "Scale", Vector(TilePulseMs, arrival - 60f, (0f, Vector3.One, null), (0.32f, Uniform(strength), _easeOutCubic), (1f, Vector3.One, _easeInOutSine)));
            }

            StartShine(ShockwaveStartMs + 60f);

            // 6. Alive.
            StartIdle(IdleStartMs);
        }

        /// <summary>
        /// A short entrance for repeat visits of the Welcome page.
        /// </summary>
        public void PlaySettle()
        {
            _entered = true;

            Animate(_logoRig, "Opacity", Scalar(SettleMs * 0.6f, 0f, (0f, 0f, null), (1f, 1f, _easeOutCubic)));
            Animate(_logoRig, "Scale", Vector(SettleMs, 0f, (0f, Uniform(0.85f), null), (1f, Vector3.One, _easeOutExpo)));
            _logoRig.StopAnimation("Offset");
            _logoRig.Offset = Vector3.Zero;
            _logoPulse.StopAnimation("Scale");
            _logoPulse.Scale = Vector3.One;

            Animate(_dome, "Opacity", Scalar(SettleMs, 0f, (0f, 0f, null), (1f, 1f, _easeOutCubic)));
            ResetDome();
            _flash.StopAnimation("Opacity");
            _flash.Opacity = 0f;
            _ring.StopAnimation("Opacity");
            _ring.Opacity = 0f;

            foreach (var glow in _glows)
            {
                Animate(glow.Sprite, "Opacity", Scalar(SettleMs * 2f, 0f, (0f, 0f, null), (1f, _palette.AuroraRestOpacity, _easeOutCubic)));
                glow.Sprite.StopAnimation("Scale");
                glow.Sprite.StopAnimation("Offset");
                glow.Sprite.Scale = Vector3.One;
                glow.Sprite.Offset = new Vector3(glow.Home, 0f);
            }

            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                var delay = i * SettleStaggerMs;
                tile.Body.Properties.StopAnimation("P");
                tile.Body.Properties.InsertScalar("P", 1f);
                tile.Floater.StopAnimation("Scale");
                tile.Floater.Scale = Vector3.One;
                Animate(tile.Body, "Opacity", Scalar(SettleMs * 0.7f, delay, (0f, 0f, null), (1f, tile.Layout.Opacity, _easeOutCubic)));
                Animate(tile.Body, "Scale", Vector(SettleMs, delay, (0f, Uniform(0.82f), null), (1f, Vector3.One, _easeOutExpo)));
            }

            StartShine(SettleMs + 250f);
            StartIdle(SettleMs);
        }

        /// <summary>
        /// Shows the final composition without any motion (used when Windows animations are turned off).
        /// </summary>
        public void ShowFinalState()
        {
            _entered = true;

            Stop(_logoRig, "Opacity", "Scale", "Offset");
            _logoRig.Opacity = 1f;
            _logoRig.Scale = Vector3.One;
            _logoRig.Offset = Vector3.Zero;
            Stop(_logoPulse, "Scale");
            _logoPulse.Scale = Vector3.One;
            Stop(_aura, "Opacity", "Scale");
            _aura.Opacity = 0.7f;
            _aura.Scale = Vector3.One;

            Stop(_dome, "Opacity");
            _dome.Opacity = 1f;
            ResetDome();
            Stop(_flash, "Opacity", "Scale");
            _flash.Opacity = 0f;
            Stop(_ring, "Opacity");
            _ring.Opacity = 0f;

            foreach (var glow in _glows)
            {
                Stop(glow.Sprite, "Opacity", "Scale", "Offset");
                Stop(glow.Drift, "Offset", "Opacity");
                glow.Sprite.Opacity = _palette.AuroraRestOpacity;
                glow.Sprite.Scale = Vector3.One;
                glow.Sprite.Offset = new Vector3(glow.Home, 0f);
            }

            foreach (var tile in _tiles)
            {
                tile.Body.Properties.StopAnimation("P");
                tile.Body.Properties.InsertScalar("P", 1f);
                Stop(tile.Body, "Opacity", "Scale");
                tile.Body.Opacity = tile.Layout.Opacity;
                tile.Body.Scale = Vector3.One;
                Stop(tile.Floater, "Scale", "Offset");
                tile.Floater.Scale = Vector3.One;
                tile.Floater.Offset = Vector3.Zero;
            }

            _shineBrush.StopAnimation("Offset");
            _shineBrush.Offset = new Vector2(-WelcomeHeroLayout.LogoSize * 1.2f, 0f);
        }

        /// <summary>
        /// Wires the pointer to the scene: a dock-like magnification field and a subtle 3D tilt.
        /// All of it runs on the compositor thread.
        /// </summary>
        public void EnablePointerEffects(CompositionPropertySet pointer)
        {
            const string near = "Square(1 - Square(Min(Length(pointer.Position.XY - stage.Offset.XY - this.Target.Offset.XY) / h.Radius, 1)))";
            foreach (var tile in _tiles)
            {
                if (tile.IsGhost)
                {
                    continue;
                }

                var magnify = Track(_compositor.CreateExpressionAnimation($"Vector3(1, 1, 1) + Vector3(1, 1, 0) * (h.Boost * h.Strength * {near})"));
                magnify.SetReferenceParameter("pointer", pointer);
                magnify.SetReferenceParameter("stage", _stage);
                magnify.SetReferenceParameter("h", _interaction);
                tile.Slot.StartAnimation("Scale", magnify);
            }

            var tiltY = Track(_compositor.CreateExpressionAnimation("h.Strength * h.MaxTilt * Clamp((pointer.Position.X - stage.Offset.X) / h.HalfWidth, -1, 1)"));
            tiltY.SetReferenceParameter("pointer", pointer);
            tiltY.SetReferenceParameter("stage", _stage);
            tiltY.SetReferenceParameter("h", _interaction);
            _tiltY.StartAnimation("RotationAngleInDegrees", tiltY);

            var tiltX = Track(_compositor.CreateExpressionAnimation("-h.Strength * h.MaxTilt * Clamp((pointer.Position.Y - stage.Offset.Y + h.CenterY) / h.HalfHeight, -1, 1)"));
            tiltX.SetReferenceParameter("pointer", pointer);
            tiltX.SetReferenceParameter("stage", _stage);
            tiltX.SetReferenceParameter("h", _interaction);
            _tiltX.StartAnimation("RotationAngleInDegrees", tiltX);

            var parallax = Track(_compositor.CreateExpressionAnimation(
                "Vector3(-h.Strength * 18 * Clamp((pointer.Position.X - stage.Offset.X) / h.HalfWidth, -1, 1), -h.Strength * 8 * Clamp((pointer.Position.Y - stage.Offset.Y + h.CenterY) / h.HalfHeight, -1, 1), 0)"));
            parallax.SetReferenceParameter("pointer", pointer);
            parallax.SetReferenceParameter("stage", _stage);
            parallax.SetReferenceParameter("h", _interaction);
            _auroraParallax.StartAnimation("Offset", parallax);
        }

        /// <summary>
        /// Eases the pointer effects in (pointer over the hero) or out.
        /// </summary>
        public void SetPointerActive(bool active)
        {
            Animate(_interaction, "Strength", Scalar(active ? 400f : 700f, 0f, (1f, active ? 1f : 0f, _easeOutCubic)));
        }

        /// <summary>
        /// Outlines the tile at <paramref name="index"/> in the logo colors (-1 for none).
        /// </summary>
        public void SetHighlightedTile(int index)
        {
            if (index == _highlighted)
            {
                return;
            }

            if (_highlighted >= 0)
            {
                Animate(_tiles[_highlighted].Highlight, "Opacity", Scalar(260f, 0f, (1f, 0f, _easeOutCubic)));
            }

            _highlighted = index;
            if (index >= 0)
            {
                Animate(_tiles[index].Highlight, "Opacity", Scalar(140f, 0f, (1f, 1f, _easeOutCubic)));
            }
        }

        public void ApplyPalette(WelcomeHeroPalette palette, bool animate)
        {
            _palette = palette;
            ApplyEffectsVisibility();

            for (var i = 0; i < _domeStops.Length; i++)
            {
                SetColor(_domeStops[i], WithAlpha(palette.Dome, _domeStopAlphas[i]), animate);
            }

            SetColor(_tileFillTop, palette.TileFillTop, animate);
            SetColor(_tileFillBottom, palette.TileFillBottom, animate);
            SetColor(_tileStroke, palette.TileStroke, animate);
            SetColor(_ghostFill, palette.GhostFill, animate);
            SetColor(_ghostStroke, palette.GhostStroke, animate);

            var flashAlphas = FlashAlphas;
            for (var i = 0; i < _flashStops.Length; i++)
            {
                SetColor(_flashStops[i], WithAlpha(palette.Flash, flashAlphas[i]), animate);
            }

            for (var i = 0; i < _highlightStops.Length && i < palette.Highlight.Length; i++)
            {
                SetColor(_highlightStops[i], palette.Highlight[i], animate);
            }

            var auraAlphas = new[] { 1f, 0.4f, 0f };
            for (var i = 0; i < _auraStops.Length; i++)
            {
                SetColor(_auraStops[i], WithAlpha(palette.Aura, auraAlphas[i]), animate);
            }

            var brandColors = WelcomeHeroPalette.BrandColors;
            for (var g = 0; g < _glows.Length; g++)
            {
                var glowAlphas = GlowAlphas(palette.GlowAlpha);
                for (var i = 0; i < _glows[g].Stops.Length; i++)
                {
                    SetColor(_glows[g].Stops[i], WithAlpha(brandColors[g], glowAlphas[i]), animate);
                }

                if (!_entered)
                {
                    continue;
                }

                if (animate)
                {
                    Animate(_glows[g].Sprite, "Opacity", Scalar(300f, 0f, (1f, palette.AuroraRestOpacity, _easeOutCubic)));
                }
                else
                {
                    _glows[g].Sprite.StopAnimation("Opacity");
                    _glows[g].Sprite.Opacity = palette.AuroraRestOpacity;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Root.Children.RemoveAll();
            for (var i = _resources.Count - 1; i >= 0; i--)
            {
                _resources[i].Dispose();
            }

            _resources.Clear();
        }

        private static Color WithAlpha(Color color, float alpha) =>
            Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(alpha, 0f, 1f)), color.R, color.G, color.B);

        private static Vector3 Uniform(float value) => new(value, value, 1f);

        private static float EaseOutCubic(float t) => 1f - ((1f - t) * (1f - t) * (1f - t));

        private static float[] GlowAlphas(float peak) => [peak, peak * 0.55f, peak * 0.2f, 0f];

        // Deterministic 0..1 noise, so every launch has the same organic jitter.
        private static float Hash(int i)
        {
            var x = MathF.Sin((i + 1) * 12.9898f) * 43758.5453f;
            return x - MathF.Floor(x);
        }

        private static void Stop(CompositionObject target, params string[] properties)
        {
            foreach (var property in properties)
            {
                target.StopAnimation(property);
            }
        }

        private float MinDistance()
        {
            var min = float.MaxValue;
            foreach (var tile in _tiles)
            {
                min = Math.Min(min, tile.Layout.Distance);
            }

            return min;
        }

        private void StartIdle(float startMs)
        {
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                var period = 2300f + (Hash(i + 101) * 1300f);
                var bob = CreateAnimation<Vector3KeyFrameAnimation>(period, startMs + (Hash(i + 57) * 1600f));
                bob.InsertKeyFrame(0f, Vector3.Zero, _linear);
                bob.InsertKeyFrame(1f, new Vector3(0f, -BobAmplitude, 0f), _easeInOutSine);
                bob.IterationBehavior = AnimationIterationBehavior.Forever;
                bob.Direction = AnimationDirection.Alternate;
                Animate(tile.Floater, "Offset", bob);
            }

            for (var i = 0; i < _glows.Length; i++)
            {
                var glow = _glows[i];
                var period = 11000f + (Hash(i + 7) * 7000f);
                var drift = CreateAnimation<Vector3KeyFrameAnimation>(period, startMs);
                drift.InsertKeyFrame(0f, Vector3.Zero, _linear);
                drift.InsertKeyFrame(1f, new Vector3((Hash(i + 3) - 0.5f) * 120f, (Hash(i + 5) - 0.5f) * 50f, 0f), _easeInOutSine);
                drift.IterationBehavior = AnimationIterationBehavior.Forever;
                drift.Direction = AnimationDirection.Alternate;
                Animate(glow.Drift, "Offset", drift);

                var breathe = Scalar(period * 0.7f, startMs, (0f, 1f, null), (1f, 0.55f, _easeInOutSine));
                breathe.IterationBehavior = AnimationIterationBehavior.Forever;
                breathe.Direction = AnimationDirection.Alternate;
                Animate(glow.Drift, "Opacity", breathe);
            }

            var auraOpacity = Scalar(3400f, Math.Max(startMs - 1500f, 0f), (0f, 0.55f, null), (1f, 1f, _easeInOutSine));
            auraOpacity.IterationBehavior = AnimationIterationBehavior.Forever;
            auraOpacity.Direction = AnimationDirection.Alternate;
            Animate(_aura, "Opacity", auraOpacity);

            var auraScale = Vector(3400f, Math.Max(startMs - 1500f, 0f), (0f, Uniform(0.92f), null), (1f, Uniform(1.1f), _easeInOutSine));
            auraScale.IterationBehavior = AnimationIterationBehavior.Forever;
            auraScale.Direction = AnimationDirection.Alternate;
            Animate(_aura, "Scale", auraScale);
        }

        private void ResetDome()
        {
            Stop(_domeBrush, "EllipseCenter", "EllipseRadius");
            _domeBrush.EllipseCenter = DomeCenter;
            _domeBrush.EllipseRadius = DomeRadius;
        }

        private void StartShine(float delayMs)
        {
            var travel = WelcomeHeroLayout.LogoSize * 1.2f;
            var shine = CreateAnimation<Vector2KeyFrameAnimation>(ShineIntervalMs, delayMs);
            shine.InsertKeyFrame(0f, new Vector2(-travel, 0f), _linear);
            shine.InsertKeyFrame(0.12f, new Vector2(travel, 0f), _easeInOutSine);
            shine.InsertKeyFrame(1f, new Vector2(travel, 0f), _linear);
            shine.IterationBehavior = AnimationIterationBehavior.Forever;
            Animate(_shineBrush, "Offset", shine);
        }

        private void ApplyEffectsVisibility()
        {
            _auroraRig.IsVisible = _palette.ShowEffects;
            _ring.IsVisible = _palette.ShowEffects;
            _aura.IsVisible = _palette.ShowEffects;
        }

        private Tile CreateTile(WelcomeHeroSlot slot, LoadedImageSurface icon, CompositionBrush tileFill, CompositionBrush highlightBrush, float highlightExtent)
        {
            const float size = WelcomeHeroLayout.TileSize;
            var isGhost = icon is null;

            var slotVisual = Track(_compositor.CreateContainerVisual());
            slotVisual.Offset = new Vector3(slot.Offset, 0f);

            var floater = Track(_compositor.CreateContainerVisual());
            slotVisual.Children.InsertAtTop(floater);

            var body = Track(_compositor.CreateContainerVisual());
            floater.Children.InsertAtTop(body);

            var glassGeometry = Track(_compositor.CreateRoundedRectangleGeometry());
            glassGeometry.Offset = new Vector2(0.5f, 0.5f);
            glassGeometry.Size = new Vector2(size - 1f);
            glassGeometry.CornerRadius = new Vector2(WelcomeHeroLayout.TileCornerRadius);
            var glassShape = Track(_compositor.CreateSpriteShape(glassGeometry));
            glassShape.FillBrush = isGhost ? _ghostFill : tileFill;
            glassShape.StrokeBrush = isGhost ? _ghostStroke : _tileStroke;
            glassShape.StrokeThickness = 1f;
            var glass = Track(_compositor.CreateShapeVisual());
            glass.Size = new Vector2(size);
            glass.Offset = new Vector3(-size / 2f, -size / 2f, 0f);
            glass.Shapes.Add(glassShape);
            body.Children.InsertAtTop(glass);

            var highlightGeometry = Track(_compositor.CreateRoundedRectangleGeometry());
            highlightGeometry.Offset = new Vector2(1.5f, 1.5f);
            highlightGeometry.Size = new Vector2(highlightExtent - 3f);
            highlightGeometry.CornerRadius = new Vector2(WelcomeHeroLayout.TileCornerRadius + 2f);
            var highlightShape = Track(_compositor.CreateSpriteShape(highlightGeometry));
            highlightShape.StrokeBrush = highlightBrush;
            highlightShape.StrokeThickness = 2f;
            var highlight = Track(_compositor.CreateShapeVisual());
            highlight.Size = new Vector2(highlightExtent);
            highlight.Offset = new Vector3(-highlightExtent / 2f, -highlightExtent / 2f, 0f);
            highlight.Opacity = 0f;
            highlight.Shapes.Add(highlightShape);

            if (!isGhost)
            {
                const float iconSize = WelcomeHeroLayout.IconSize;
                var iconVisual = Track(_compositor.CreateSpriteVisual());
                iconVisual.Brush = CreateSurfaceBrush(icon);
                iconVisual.Size = new Vector2(iconSize);
                iconVisual.Offset = new Vector3(-iconSize / 2f, -iconSize / 2f, 0f);
                body.Children.InsertAtTop(iconVisual);
                body.Children.InsertAtTop(highlight);

                // The flight: a quadratic Bezier from the logo (A) to the tile (origin), bulging past the
                // tile (C), so tools shoot out like a fountain and rain down into place.
                var start = new Vector3(-slot.Offset, FlightDepth);
                var control = new Vector3(start.X * 0.8f, start.Y * -0.25f, FlightDepth * 0.2f);
                var path = Track(_compositor.CreateExpressionAnimation("A * Square(1 - t.P) + C * (2 * (1 - t.P) * t.P)"));
                path.SetReferenceParameter("t", body.Properties);
                path.SetVector3Parameter("A", start);
                path.SetVector3Parameter("C", control);
                body.Properties.InsertScalar("P", 0f);
                body.StartAnimation("Offset", path);

                var spin = Track(_compositor.CreateExpressionAnimation("R * (1 - t.P)"));
                spin.SetReferenceParameter("t", body.Properties);
                spin.SetScalarParameter("R", (slot.Column < 0 ? -1f : 1f) * (12f + (Hash(slot.Rank + 31) * 22f)));
                body.StartAnimation("RotationAngleInDegrees", spin);
            }
            else
            {
                body.Properties.InsertScalar("P", 1f);
            }

            return new Tile(slot, isGhost, slotVisual, floater, body, highlight);
        }

        private Glow CreateGlow(Color color, float alpha, Vector2 home)
        {
            var drift = Track(_compositor.CreateContainerVisual());
            var (sprite, stops) = CreateRadialSprite(GlowSize, color, GlowAlphas(alpha), [0f, 0.35f, 0.7f, 1f]);
            sprite.CenterPoint = new Vector3(GlowSize / 2f, 0f);

            // Home and origin are top-left positions: the glow is centered on its target point.
            var centeredHome = new Vector2(home.X, _layout.ApexY + home.Y) - (GlowSize / 2f);
            var origin = new Vector2(0f, _layout.ApexY) - (GlowSize / 2f);
            sprite.Offset = new Vector3(centeredHome, 0f);
            drift.Children.InsertAtTop(sprite);
            return new Glow(drift, sprite, stops, centeredHome, origin);
        }

        private (SpriteVisual Sprite, CompositionColorGradientStop[] Stops) CreateRadialSprite(Vector2 size, Color color, float[] alphas, float[] offsets)
        {
            var brush = Track(_compositor.CreateRadialGradientBrush());
            var stops = new CompositionColorGradientStop[alphas.Length];
            for (var i = 0; i < alphas.Length; i++)
            {
                stops[i] = Track(_compositor.CreateColorGradientStop(offsets[i], WithAlpha(color, alphas[i])));
                brush.ColorStops.Add(stops[i]);
            }

            var sprite = Track(_compositor.CreateSpriteVisual());
            sprite.Brush = brush;
            sprite.Size = size;
            return (sprite, stops);
        }

        private CompositionSurfaceBrush CreateSurfaceBrush(LoadedImageSurface surface)
        {
            var brush = Track(_compositor.CreateSurfaceBrush(surface));
            brush.Stretch = CompositionStretch.Uniform;
            brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.Linear;
            return brush;
        }

        private void SetColor(CompositionObject target, Color color, bool animate)
        {
            if (animate)
            {
                var animation = CreateAnimation<ColorKeyFrameAnimation>(300f, 0f);
                animation.InsertKeyFrame(1f, color, _easeOutCubic);
                Animate(target, "Color", animation);
                return;
            }

            target.StopAnimation("Color");
            switch (target)
            {
                case CompositionColorBrush brush:
                    brush.Color = color;
                    break;
                case CompositionColorGradientStop stop:
                    stop.Color = color;
                    break;
            }
        }

        private ScalarKeyFrameAnimation Scalar(float durationMs, float delayMs, params (float Progress, float Value, CompositionEasingFunction Easing)[] frames)
        {
            var animation = CreateAnimation<ScalarKeyFrameAnimation>(durationMs, delayMs);
            foreach (var (progress, value, easing) in frames)
            {
                animation.InsertKeyFrame(progress, value, easing ?? _linear);
            }

            return animation;
        }

        private Vector3KeyFrameAnimation Vector(float durationMs, float delayMs, params (float Progress, Vector3 Value, CompositionEasingFunction Easing)[] frames)
        {
            var animation = CreateAnimation<Vector3KeyFrameAnimation>(durationMs, delayMs);
            foreach (var (progress, value, easing) in frames)
            {
                animation.InsertKeyFrame(progress, value, easing ?? _linear);
            }

            return animation;
        }

        private Vector2KeyFrameAnimation Vector2D(float durationMs, float delayMs, params (float Progress, Vector2 Value, CompositionEasingFunction Easing)[] frames)
        {
            var animation = CreateAnimation<Vector2KeyFrameAnimation>(durationMs, delayMs);
            foreach (var (progress, value, easing) in frames)
            {
                animation.InsertKeyFrame(progress, value, easing ?? _linear);
            }

            return animation;
        }

        private T CreateAnimation<T>(float durationMs, float delayMs)
            where T : KeyFrameAnimation
        {
            KeyFrameAnimation animation = typeof(T) switch
            {
                var t when t == typeof(ScalarKeyFrameAnimation) => _compositor.CreateScalarKeyFrameAnimation(),
                var t when t == typeof(Vector2KeyFrameAnimation) => _compositor.CreateVector2KeyFrameAnimation(),
                var t when t == typeof(Vector3KeyFrameAnimation) => _compositor.CreateVector3KeyFrameAnimation(),
                var t when t == typeof(ColorKeyFrameAnimation) => _compositor.CreateColorKeyFrameAnimation(),
                _ => throw new NotSupportedException(typeof(T).Name),
            };

            animation.Duration = TimeSpan.FromMilliseconds(Math.Max(durationMs, 1f));
            animation.DelayTime = TimeSpan.FromMilliseconds(Math.Max(delayMs, 0f));
            animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            return Track((T)animation);
        }

        private void Animate(CompositionObject target, string property, CompositionAnimation animation)
        {
            if (!_disposed)
            {
                target.StartAnimation(property, animation);
            }
        }

        private T Track<T>(T resource)
            where T : IDisposable
        {
            _resources.Add(resource);
            return resource;
        }

        private sealed record Tile(WelcomeHeroSlot Layout, bool IsGhost, ContainerVisual Slot, ContainerVisual Floater, ContainerVisual Body, ShapeVisual Highlight);

        private sealed record Glow(ContainerVisual Drift, SpriteVisual Sprite, CompositionColorGradientStop[] Stops, Vector2 Home, Vector2 Origin);
    }
}

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
    /// The Composition visual tree behind the Welcome hero ("Warp").
    /// <para>
    /// Every tool drops out of hyperspace: the tiles rush in from deep behind the logo trailing light streaks
    /// in the logo colors, through a field of stars that races past the viewer. The PowerToys logo slams in
    /// last, the scene shakes, a flash and a ring in the logo colors burst out of it, and the dome of light
    /// rises behind it like a sunrise. Afterwards the scene stays alive: tiles float, the aurora drifts, and
    /// the pointer tilts the scene and magnifies nearby tiles.
    /// </para>
    /// <para>
    /// Every animated property lives on its own visual level (slot = magnification, floater = idle bob and
    /// impact pulse, body = warp flight), so the whole timeline starts in a single frame using delays.
    /// </para>
    /// </summary>
    internal sealed partial class WelcomeHeroScene : IDisposable
    {
        /// <summary>
        /// Scale of a tile right under the pointer.
        /// </summary>
        internal const float MaxMagnification = 1.26f;

        /// <summary>
        /// Time at which the page text starts to rise into view (intro), right after the logo has landed.
        /// </summary>
        internal const float TextRevealStartMs = ImpactMs + 60f;

        // Intro timeline, in milliseconds. The logo lands (impact) at SlamStartMs + SlamMs.
        private const float FlightMs = 1150f;
        private const float LaunchStaggerMs = 11f;
        private const float SlamStartMs = 780f;
        private const float SlamMs = 240f;
        private const float ImpactMs = SlamStartMs + SlamMs;
        private const float ShakeMs = 320f;
        private const float FlashMs = 620f;
        private const float DomeMs = 1100f;
        private const float GlowMs = 1500f;
        private const float RingMs = 1000f;
        private const float PulseSpreadMs = 900f;
        private const float TilePulseMs = 560f;
        private const float ShineIntervalMs = 7000f;
        private const float IdleStartMs = 2100f;

        // Settle timeline (repeat visits), in milliseconds.
        private const float SettleMs = 450f;
        private const float SettleStaggerMs = 7f;

        // The warp: depths are in DIPs along the z axis (negative = away from the viewer).
        private const float WarpDepth = 3600f;
        private const float SlamScale = 2.6f;
        private const float ShakeAmplitude = 3f;
        private const float TilePulse = 1.1f;
        private const float RingOpacity = 0.4f;
        private const float RingThickness = 8f;

        // Light streaks behind the tiles and the star field.
        private const float TrailLagMs = 55f;
        private const float TrailOpacity = 0.75f;
        private const float TrailThickness = 18f;
        private const int StarCount = 70;
        private const float StarOpacity = 0.8f;
        private const float StarTravel = 6200f;
        private const float StarFadeInMs = 90f;
        private const float StarCullDepth = 620f;

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
        private readonly CompositionEasingFunction _easeWarp;
        private readonly CompositionEasingFunction _easeOutBack;

        private readonly RectangleClip _clip;
        private readonly ContainerVisual _shake;
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
        private readonly ContainerVisual _trails;
        private readonly Dictionary<Color, CompositionBrush> _trailBrushes = [];
        private readonly CompositionBrush _starlightBrush;
        private readonly CompositionColorGradientStop[] _starlightStops;
        private readonly WelcomeHeroWarp.Star[] _stars;
        private readonly Trail[] _starTrails;
        private readonly Trail[] _tileTrails;
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
        private int _introGeneration;
        private bool _trailsActive;
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

            // Blasts out of the depth and brakes hard: most of the distance is covered in the first frames.
            _easeWarp = Track(compositor.CreateCubicBezierEasingFunction(new Vector2(0.05f, 0.8f), new Vector2(0.1f, 1f)));
            _easeOutBack = Track(CompositionEasingFunction.CreateBackEasingFunction(compositor, CompositionEasingFunctionMode.Out, 1.6f));

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

            // Everything but the clip shakes when the logo lands.
            _shake = Track(compositor.CreateContainerVisual());
            Root.Children.InsertAtTop(_shake);

            // --- Aurora: the dome of light and the brand colored glows behind everything. ---
            _auroraRig = Track(compositor.CreateContainerVisual());
            _auroraParallax = Track(compositor.CreateContainerVisual());
            _auroraRig.Children.InsertAtTop(_auroraParallax);
            _shake.Children.InsertAtTop(_auroraRig);

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
            _stage.TransformMatrix = WelcomeHeroWarp.CreateStageTransform(funnelCenter);
            _shake.Children.InsertAtTop(_stage);

            // Light streaks live flat on the stage (depth 0 is untouched by the perspective), below the tilt,
            // in a layer centered on the vanishing point. Stars first, so the tile streaks draw on top.
            _trails = Track(compositor.CreateContainerVisual());
            _trails.Offset = new Vector3(WelcomeHeroWarp.GetVanishingPoint(funnelCenter), 0f);
            _trails.IsVisible = false;
            _stage.Children.InsertAtTop(_trails);

            var warpMs = FlightMs + (layout.Slots.Count * LaunchStaggerMs);
            (_starlightBrush, _starlightStops) = CreateTrailBrush(palette.Starlight);
            _stars = WelcomeHeroWarp.CreateStars(StarCount, funnelCenter, warpMs);
            _starTrails = new Trail[_stars.Length];
            for (var i = 0; i < _stars.Length; i++)
            {
                var star = _stars[i];
                _starTrails[i] = CreateTrail(
                    star.Ray,
                    star.Thickness,
                    star.Color is { } color ? GetTrailBrush(color) : _starlightBrush,
                    "Peak * Min(1, d.Life * FadeIn) * Pow(1 - d.Life, 1.4) * Clamp((Cull - d.Head) / 40, 0, 1)",
                    StarOpacity);
                _starTrails[i].Opacity.SetScalarParameter("FadeIn", star.DurationMs / StarFadeInMs);
                _starTrails[i].Opacity.SetScalarParameter("Cull", StarCullDepth);
            }

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
            _tileTrails = new Trail[_tiles.Length];
            for (var i = 0; i < _tiles.Length; i++)
            {
                var slot = layout.Slots[i];
                var icon = i < icons.Count ? icons[i] : null;
                _tiles[i] = CreateTile(slot, icon, tileFill, highlightBrush, highlightExtent);
                _tiltX.Children.InsertAtTop(_tiles[i].Slot);

                _tileTrails[i] = CreateTrail(
                    WelcomeHeroWarp.GetRay(slot.Offset, funnelCenter),
                    TrailThickness,
                    GetTrailBrush(WelcomeHeroWarp.BrandColorAt(slot.Offset.X)),
                    "Peak * Pow(Clamp(-d.Head / Depth, 0, 1), 0.6)",
                    TrailOpacity * slot.Opacity);
                _tileTrails[i].Opacity.SetScalarParameter("Depth", WarpDepth);
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

            StopTrails();
        }

        /// <summary>
        /// Plays the full "Warp" intro. Everything is scheduled in a single frame.
        /// </summary>
        public void PlayIntro()
        {
            _entered = true;
            var generation = ++_introGeneration;

            Stop(_logoRig, "Offset");
            _logoRig.Offset = Vector3.Zero;
            foreach (var tile in _tiles)
            {
                Stop(tile.Body, "Scale");
                tile.Body.Scale = Vector3.One;
            }

            // 1. Warp: every tool drops out of hyperspace, trailing light, through a field of rushing stars.
            //    The streaks are only needed during the flight, so they are switched off once it is over.
            StartTrails();
            var flight = Track(_compositor.CreateScopedBatch(CompositionBatchTypes.Animation));
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                var launch = i * LaunchStaggerMs;
                Animate(tile.Body, "Offset", Vector(FlightMs, launch, (0f, new Vector3(0f, 0f, -WarpDepth), null), (1f, Vector3.Zero, _easeWarp)));
                Animate(tile.Body, "Opacity", Scalar(180f, launch, (0f, 0f, null), (1f, tile.Layout.Opacity, _easeOutCubic)));

                var depth = _tileTrails[i].Depth;
                Animate(depth, "Head", Scalar(FlightMs, launch, (0f, -WarpDepth, null), (1f, 0f, _easeWarp)));
                Animate(depth, "Tail", Scalar(FlightMs, launch + TrailLagMs, (0f, -WarpDepth, null), (1f, 0f, _easeWarp)));
            }

            for (var i = 0; i < _stars.Length; i++)
            {
                var star = _stars[i];
                var depth = _starTrails[i].Depth;
                var end = star.StartZ + StarTravel;
                Animate(depth, "Head", Scalar(star.DurationMs, star.DelayMs, (0f, star.StartZ, null), (1f, end, _easeWarp)));
                Animate(depth, "Tail", Scalar(star.DurationMs, star.DelayMs + TrailLagMs, (0f, star.StartZ, null), (1f, end, _easeWarp)));
                Animate(depth, "Life", Scalar(star.DurationMs, star.DelayMs, (0f, 0f, null), (1f, 1f, null)));
            }

            flight.End();
            flight.Completed += (_, _) =>
            {
                if (!_disposed && generation == _introGeneration)
                {
                    StopTrails();
                }
            };

            // 2. Slam: the logo drops in last, hits the stage and pops back.
            Animate(_logoRig, "Opacity", Scalar(90f, SlamStartMs, (0f, 0f, null), (1f, 1f, null)));
            Animate(_logoRig, "Scale", Vector(SlamMs, SlamStartMs, (0f, Uniform(SlamScale), null), (1f, Vector3.One, _easeInQuad)));
            Animate(_logoPulse, "Scale", Vector(460f, ImpactMs, (0f, Vector3.One, null), (0.2f, Uniform(0.92f), _easeOutCubic), (1f, Vector3.One, _easeOutBack)));

            const float k = ShakeAmplitude;
            Animate(_shake, "Offset", Vector(
                ShakeMs,
                ImpactMs,
                (0f, Vector3.Zero, null),
                (0.1f, new Vector3(k, -k * 0.7f, 0f), null),
                (0.3f, new Vector3(-k * 0.75f, k * 0.45f, 0f), null),
                (0.55f, new Vector3(k * 0.35f, -k * 0.2f, 0f), null),
                (1f, Vector3.Zero, _easeInOutSine)));

            // 3. Impact: a white-hot flash, and the four logo colors bursting out as light.
            Animate(_flash, "Opacity", Scalar(FlashMs, ImpactMs, (0f, 0f, null), (0.1f, 1f, _easeOutCubic), (1f, 0f, _easeInQuad)));
            Animate(_flash, "Scale", Vector(FlashMs, ImpactMs, (0f, Uniform(0.3f), null), (1f, Uniform(2.3f), _easeOutExpo)));

            for (var i = 0; i < _glows.Length; i++)
            {
                var glow = _glows[i];
                var delay = ImpactMs + (Math.Abs(i - 1.5f) * 40f);
                Animate(glow.Sprite, "Opacity", Scalar(GlowMs, delay, (0f, 0f, null), (0.22f, 1f, _easeOutCubic), (1f, _palette.AuroraRestOpacity, _easeInOutSine)));
                Animate(glow.Sprite, "Scale", Vector(GlowMs, delay, (0f, Uniform(0.15f), null), (1f, Vector3.One, _easeOutExpo)));
                Animate(glow.Sprite, "Offset", Vector(GlowMs, delay, (0f, new Vector3(glow.Origin, 0f), null), (1f, new Vector3(glow.Home, 0f), _easeOutExpo)));
            }

            // 4. Sunrise: the dome of light grows out of the logo.
            var dawnStart = ImpactMs - 40f;
            var dawnCenter = new Vector2(DomeCenter.X, _layout.ApexY);
            Animate(_dome, "Opacity", Scalar(200f, dawnStart, (0f, 0f, null), (1f, 1f, null)));
            Animate(_domeBrush, "EllipseCenter", Vector2D(DomeMs, dawnStart, (0f, dawnCenter, null), (1f, DomeCenter, _easeOutExpo)));
            Animate(_domeBrush, "EllipseRadius", Vector2D(DomeMs, dawnStart, (0f, new Vector2(90f, 60f), null), (1f, DomeRadius, _easeOutExpo)));

            // 5. Shockwave: a ring in the logo colors ripples out of the logo and bumps every tile it passes.
            var ringStart = WelcomeHeroLayout.LogoSize / 2f;
            var ringEnd = _layout.MaxSlotDistance + 120f;
            var radius = CreateAnimation<Vector2KeyFrameAnimation>(RingMs, ImpactMs);
            for (var step = 0; step <= 12; step++)
            {
                var t = step / 12f;
                radius.InsertKeyFrame(t, new Vector2(ringStart + ((ringEnd - ringStart) * EaseOutCubic(t))), _linear);
            }

            Animate(_ringGeometry, "Radius", radius);
            Animate(_ringShape, "StrokeThickness", Scalar(RingMs, ImpactMs, (0f, RingThickness, null), (1f, 1f, _easeOutCubic)));
            Animate(_ring, "Opacity", Scalar(RingMs, ImpactMs, (0f, 0f, null), (0.04f, RingOpacity, null), (0.55f, RingOpacity * 0.53f, null), (1f, 0f, _easeInOutSine)));

            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                var reach = Math.Clamp((tile.Layout.Distance - ringStart) / (ringEnd - ringStart), 0f, 1f);
                var arrival = ImpactMs + (PulseSpreadMs * (1f - MathF.Cbrt(1f - reach)));
                var strength = tile.IsGhost ? 1f + ((TilePulse - 1f) / 2f) : TilePulse;
                Animate(tile.Floater, "Scale", Vector(TilePulseMs, arrival - 60f, (0f, Vector3.One, null), (0.32f, Uniform(strength), _easeOutCubic), (1f, Vector3.One, _easeInOutSine)));
            }

            StartShine(ImpactMs + 120f);

            // 6. Alive.
            StartIdle(IdleStartMs);
        }

        /// <summary>
        /// A short entrance for repeat visits of the Welcome page.
        /// </summary>
        public void PlaySettle()
        {
            _entered = true;
            _introGeneration++;
            StopTrails();
            Stop(_shake, "Offset");
            _shake.Offset = Vector3.Zero;

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
                Stop(tile.Body, "Offset");
                tile.Body.Offset = Vector3.Zero;
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
            _introGeneration++;
            StopTrails();
            Stop(_shake, "Offset");
            _shake.Offset = Vector3.Zero;

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
                Stop(tile.Body, "Opacity", "Scale", "Offset");
                tile.Body.Opacity = tile.Layout.Opacity;
                tile.Body.Scale = Vector3.One;
                tile.Body.Offset = Vector3.Zero;
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

            SetColor(_starlightStops[0], WithAlpha(palette.Starlight, 0f), animate);
            SetColor(_starlightStops[1], palette.Starlight, animate);

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

        private static float Hash(int i) => WelcomeHeroWarp.Hash(i);

        private static void Stop(CompositionObject target, params string[] properties)
        {
            foreach (var property in properties)
            {
                target.StopAnimation(property);
            }
        }

        private static void StopTrail(Trail trail)
        {
            Stop(trail.Sprite, "Offset", "Size", "Opacity");
            Stop(trail.Depth, "Head", "Tail", "Life");
            trail.Sprite.Opacity = 0f;
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
            _trails.IsVisible = _palette.ShowEffects && _trailsActive;
        }

        private void StartTrails()
        {
            _trailsActive = true;
            foreach (var trail in _starTrails)
            {
                StartTrail(trail);
            }

            foreach (var trail in _tileTrails)
            {
                StartTrail(trail);
            }

            ApplyEffectsVisibility();
        }

        private void StartTrail(Trail trail)
        {
            Animate(trail.Sprite, "Offset", trail.Offset);
            Animate(trail.Sprite, "Size", trail.Size);
            Animate(trail.Sprite, "Opacity", trail.Opacity);
        }

        private void StopTrails()
        {
            _trailsActive = false;
            _trails.IsVisible = false;
            foreach (var trail in _starTrails)
            {
                StopTrail(trail);
            }

            foreach (var trail in _tileTrails)
            {
                StopTrail(trail);
            }
        }

        /// <summary>
        /// Creates a light streak on <paramref name="ray"/>: a sprite that fades in from its tail and reaches from
        /// the projection of depth "Tail" to the projection of depth "Head" (both in its Depth property set).
        /// Its thickness follows the perspective of the head, just like a line drawn in 3D.
        /// </summary>
        private Trail CreateTrail(WelcomeHeroWarp.Ray ray, float thickness, CompositionBrush brush, string opacityExpression, float peakOpacity)
        {
            var rayVisual = Track(_compositor.CreateContainerVisual());
            rayVisual.RotationAngle = ray.Angle;
            _trails.Children.InsertAtTop(rayVisual);

            var sprite = Track(_compositor.CreateSpriteVisual());
            sprite.Brush = brush;
            sprite.Opacity = 0f;
            rayVisual.Children.InsertAtTop(sprite);

            var depth = Track(_compositor.CreatePropertySet());
            depth.InsertScalar("Head", 0f);
            depth.InsertScalar("Tail", 0f);
            depth.InsertScalar("Life", 1f);

            const string head = "Max(1 - d.Head / Focal, MinW)";
            const string tail = "Max(1 - d.Tail / Focal, MinW)";
            var offset = CreateTrailExpression($"Vector3(Reach / {tail}, -Width / (2 * {head}), 0)", depth, ray, thickness);
            var size = CreateTrailExpression($"Vector2(Reach / {head} - Reach / {tail}, Width / {head})", depth, ray, thickness);
            var opacity = CreateTrailExpression(opacityExpression, depth, ray, thickness);
            opacity.SetScalarParameter("Peak", peakOpacity);

            return new Trail(sprite, depth, offset, size, opacity);
        }

        private ExpressionAnimation CreateTrailExpression(string expression, CompositionPropertySet depth, WelcomeHeroWarp.Ray ray, float thickness)
        {
            var animation = Track(_compositor.CreateExpressionAnimation(expression));
            animation.SetReferenceParameter("d", depth);
            animation.SetScalarParameter("Reach", ray.Length);
            animation.SetScalarParameter("Width", thickness);
            animation.SetScalarParameter("Focal", WelcomeHeroWarp.PerspectiveDistance);
            animation.SetScalarParameter("MinW", WelcomeHeroWarp.MinPerspectiveDivisor);
            return animation;
        }

        private CompositionBrush GetTrailBrush(Color color)
        {
            if (!_trailBrushes.TryGetValue(color, out var brush))
            {
                brush = CreateTrailBrush(color).Brush;
                _trailBrushes[color] = brush;
            }

            return brush;
        }

        // A streak fades in from its tail, at the left of the sprite, to its head.
        private (CompositionBrush Brush, CompositionColorGradientStop[] Stops) CreateTrailBrush(Color color)
        {
            var gradient = Track(_compositor.CreateLinearGradientBrush());
            gradient.StartPoint = Vector2.Zero;
            gradient.EndPoint = Vector2.UnitX;
            var stops = new[]
            {
                Track(_compositor.CreateColorGradientStop(0f, WithAlpha(color, 0f))),
                Track(_compositor.CreateColorGradientStop(1f, color)),
            };
            foreach (var stop in stops)
            {
                gradient.ColorStops.Add(stop);
            }

            return (gradient, stops);
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

        private sealed record Trail(SpriteVisual Sprite, CompositionPropertySet Depth, ExpressionAnimation Offset, ExpressionAnimation Size, ExpressionAnimation Opacity);
    }
}

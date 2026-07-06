// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Awake.ViewModels;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI;

namespace Awake
{
    /// <summary>
    /// The flyout's main page: the idle/active header (with the composition glow) and the
    /// duration selection. The "Custom" and "While app runs" cards navigate the shell frame to
    /// their own pages instead of opening flyouts.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class AwakeLaunchPage : Page
    {
        private ContainerVisual? _glowRoot;
        private AwakeFlyoutNavigationContext? _context;
        private bool _subscribed;

        public AwakeLaunchPage()
        {
            InitializeComponent();

            HeaderGlowHost.Loaded += OnHeaderGlowLoaded;
            HeaderGlowHost.SizeChanged += OnHeaderGlowSizeChanged;
        }

        public AwakeFlyoutViewModel ViewModel { get; private set; } = default!;

        /// <summary>
        /// Moves keyboard focus into the page so Escape and tab navigation work.
        /// </summary>
        public void FocusContent() => RootGrid.Focus(FocusState.Programmatic);

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is AwakeFlyoutNavigationContext context)
            {
                _context = context;
                ViewModel = context.ViewModel;
                this.Bindings.Update();

                if (!_subscribed)
                {
                    ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                    _subscribed = true;
                }
            }

            // Only realign the pending selection with the running mode on a fresh open / forward
            // navigation. On Back navigation the user may have just chosen a custom duration or an
            // app on a sub-page; re-syncing here would clobber that pending selection.
            if (e.NavigationMode != NavigationMode.Back)
            {
                ViewModel?.SyncPendingFromMode();
            }

            HighlightSelectedCard();
            RefreshWhileAppVisuals();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (_subscribed && ViewModel is not null)
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribed = false;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AwakeFlyoutViewModel.Mode))
            {
                ViewModel?.SyncPendingFromMode();
                HighlightSelectedCard();
                RefreshWhileAppVisuals();
            }
            else if (e.PropertyName == nameof(AwakeFlyoutViewModel.WhileAppCardIcon))
            {
                RefreshWhileAppVisuals();
            }
        }

        // Highlights the card matching the current pending selection so the buttons and the header
        // state stay in lockstep.
        private void HighlightSelectedCard()
        {
            if (ViewModel is null)
            {
                return;
            }

            ToggleButton? selected = ViewModel.PendingSelection switch
            {
                FlyoutSelectionKind.Forever => CardForever,
                FlyoutSelectionKind.Custom => CardCustom,
                FlyoutSelectionKind.WhileApp => CardWhileApp,
                FlyoutSelectionKind.WhileAgent => CardWhileApp,
                _ => CardForMinutes(ViewModel.PendingMinutes),
            };

            SetSelectedCard(selected);
        }

        // Shows the captured app icon on the While-app card when one is available, otherwise falls
        // back to a glyph (the agent robot glyph when an agent is selected, else the generic app glyph).
        private void RefreshWhileAppVisuals()
        {
            bool hasIcon = ViewModel?.WhileAppCardIcon is not null;
            CardWhileAppIcon.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;
            CardWhileAppGlyph.Visibility = hasIcon ? Visibility.Collapsed : Visibility.Visible;

            bool isAgent = ViewModel?.PendingSelection == FlyoutSelectionKind.WhileAgent;
            CardWhileAppGlyph.Glyph = isAgent ? "\uE99A" : "\uE7F4";
        }

        private ToggleButton? CardForMinutes(uint minutes) => minutes switch
        {
            30 => Card30,
            60 => Card60,
            120 => Card120,
            _ => null,
        };

        private void SetSelectedCard(ToggleButton? selected)
        {
            foreach (ToggleButton card in new[] { Card30, Card60, Card120, CardForever, CardCustom, CardWhileApp })
            {
                card.IsChecked = ReferenceEquals(card, selected);
            }
        }

        private void OnDurationCardClick(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button
                && button.Tag is string tag
                && uint.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint minutes))
            {
                ViewModel.PendingSelection = FlyoutSelectionKind.Timed;
                ViewModel.PendingMinutes = minutes;
                SetSelectedCard(button);
                ViewModel.ApplyPendingIfActive();
            }
        }

        private void OnForeverCardClick(object sender, RoutedEventArgs e)
        {
            ViewModel.PendingSelection = FlyoutSelectionKind.Forever;
            SetSelectedCard(CardForever);
            ViewModel.ApplyPendingIfActive();
        }

        // Left segment of the Custom split tile: select the custom duration (using whatever value
        // was last configured) without leaving the launch page.
        private void OnCustomToggleClick(object sender, RoutedEventArgs e)
        {
            ViewModel.PendingSelection = FlyoutSelectionKind.Custom;
            HighlightSelectedCard();
            ViewModel.ApplyPendingIfActive();
        }

        // Right chevron of the Custom split tile: navigate to the custom-time picker.
        private void OnCustomNavClick(object sender, RoutedEventArgs e)
        {
            // Keep the custom page's tab in sync with the running session (duration vs. until-date).
            ViewModel.RefreshPendingCustomSubMode();

            if (_context != null && Frame != null)
            {
                Frame.Navigate(typeof(AwakeCustomTimePage), _context, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }

        // Left segment of the While-app split tile: select the existing app binding if one was
        // already chosen; otherwise jump straight to the picker since there is nothing to select.
        private void OnWhileAppToggleClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PendingProcessId != 0)
            {
                ViewModel.PendingSelection = FlyoutSelectionKind.WhileApp;
                HighlightSelectedCard();
                ViewModel.ApplyPendingIfActive();
            }
            else
            {
                HighlightSelectedCard();
                NavigateToAppPicker();
            }
        }

        // Right chevron of the While-app split tile: navigate to the app picker.
        private void OnWhileAppNavClick(object sender, RoutedEventArgs e) => NavigateToAppPicker();

        private void NavigateToAppPicker()
        {
            if (_context != null && Frame != null)
            {
                Frame.Navigate(typeof(AwakeAppPickerPage), _context, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }

        private void OnStartButtonClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyPendingSelection();
        }

        private void OnStopButtonClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Mode = AwakeMode.PASSIVE;
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenSettingsCommand.Execute(null);
            _context?.RequestClose();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                _context?.RequestClose();
                e.Handled = true;
            }
        }

        private void OnHeaderGlowLoaded(object sender, RoutedEventArgs e) => BuildGlow();

        private void OnHeaderGlowSizeChanged(object sender, SizeChangedEventArgs e) => LayoutGlow();

        // Builds a couple of soft accent-tinted radial "blobs" that slowly drift and pulse behind
        // the active header, giving a subtle organic glow without a flat colored fill.
        private void BuildGlow()
        {
            if (_glowRoot != null)
            {
                return;
            }

            Compositor compositor = ElementCompositionPreview.GetElementVisual(HeaderGlowHost).Compositor;
            _glowRoot = compositor.CreateContainerVisual();
            ElementCompositionPreview.SetElementChildVisual(HeaderGlowHost, _glowRoot);

            PopulateGlowBlobs(compositor);
            LayoutGlow();
        }

        // Clears and re-creates the glow blobs with the current accent color. Called when the
        // flyout opens so the glow picks up any accent/theme change that happened while hidden.
        public void RefreshGlow()
        {
            if (_glowRoot == null)
            {
                return;
            }

            _glowRoot.Children.RemoveAll();
            PopulateGlowBlobs(_glowRoot.Compositor);
            LayoutGlow();
        }

        // Builds a layered, animated Fluent "aurora" behind the active header: drifting/breathing
        // accent light clouds, brighter drifting light streaks, twinkling sparkles, and periodic
        // bright shine sweeps that glint diagonally across for a lively, premium feel.
        private void PopulateGlowBlobs(Compositor compositor)
        {
            Color accent = (Application.Current.Resources["SystemAccentColor"] is Color a) ? a : Colors.White;
            Color accentLight1 = (Application.Current.Resources["SystemAccentColorLight1"] is Color l1) ? l1 : accent;
            Color accentLight2 = (Application.Current.Resources["SystemAccentColorLight2"] is Color l2) ? l2 : accent;
            Color white = Colors.White;

            // Aurora clouds (bottom layer): (color, sizeRel, startRel, endRel, maxOpacity, seconds, delay)
            AddAurora(compositor, accentLight1, new Vector2(0.95f, 1.30f), new Vector2(0.15f, 0.30f), new Vector2(0.52f, 0.20f), 0.60f, 9, 0f);
            AddAurora(compositor, accentLight2, new Vector2(0.85f, 1.15f), new Vector2(0.82f, 0.42f), new Vector2(0.44f, 0.30f), 0.52f, 11, 2f);
            AddAurora(compositor, accent, new Vector2(0.70f, 1.00f), new Vector2(0.52f, 0.16f), new Vector2(0.74f, 0.34f), 0.34f, 13, 4f);

            // Soft, blurred drifting light streaks: (color, sizeRel, startRel, endRel, maxOpacity, minOpacity, seconds, delay, angle)
            AddStreak(compositor, white, new Vector2(1.60f, 1.05f), new Vector2(0.10f, 0.20f), new Vector2(0.55f, 0.12f), 0.42f, 0.14f, 16, 0f, -18f);
            AddStreak(compositor, accentLight2, new Vector2(1.75f, 1.20f), new Vector2(0.60f, 0.46f), new Vector2(0.98f, 0.32f), 0.40f, 0.12f, 20, 2f, -24f);

            // Shine sweeps (the "pop"): bright bands that glint across, then pause. (color, maxOpacity, seconds, delay, angle)
            AddSweep(compositor, white, 0.85f, 4.2f, 0.4f, -20f);
            AddSweep(compositor, accentLight1, 0.60f, 5.3f, 2.1f, -14f);
            AddSweep(compositor, white, 0.70f, 6.1f, 3.6f, -26f);

            // Sparkles (top layer): (color, posRel, maxOpacity, diameter, delay)
            AddSparkle(compositor, white, new Vector2(0.70f, 0.20f), 1.0f, 3.6f, 0f);
            AddSparkle(compositor, white, new Vector2(0.86f, 0.38f), 0.85f, 3.0f, 1.1f);
            AddSparkle(compositor, white, new Vector2(0.58f, 0.30f), 0.75f, 2.6f, 0.6f);
            AddSparkle(compositor, accentLight1, new Vector2(0.78f, 0.52f), 0.70f, 2.4f, 1.7f);
        }

        // A soft vertical fade (transparent top/bottom, opaque middle) used to give layers soft
        // edges and keep the glow concentrated away from the header's bottom text.
        private CompositionLinearGradientBrush VerticalFade(Compositor compositor)
        {
            var fade = compositor.CreateLinearGradientBrush();
            fade.StartPoint = new Vector2(0.5f, 0f);
            fade.EndPoint = new Vector2(0.5f, 1f);
            fade.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, 255, 255, 255)));
            fade.ColorStops.Add(compositor.CreateColorGradientStop(0.18f, Colors.White));
            fade.ColorStops.Add(compositor.CreateColorGradientStop(0.6f, Colors.White));
            fade.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 255, 255, 255)));
            return fade;
        }

        // A large, soft accent cloud that slowly drifts (LayoutGlow) and gently breathes (scale).
        private void AddAurora(Compositor compositor, Color color, Vector2 sizeRel, Vector2 startRel, Vector2 endRel, float maxOpacity, int seconds, float delay)
        {
            var radial = compositor.CreateRadialGradientBrush();
            radial.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb((byte)(maxOpacity * 255), color.R, color.G, color.B)));
            radial.ColorStops.Add(compositor.CreateColorGradientStop(0.55f, Color.FromArgb((byte)(maxOpacity * 0.45f * 255), color.R, color.G, color.B)));
            radial.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

            var mask = compositor.CreateMaskBrush();
            mask.Source = radial;
            mask.Mask = VerticalFade(compositor);

            var blob = compositor.CreateSpriteVisual();
            blob.Brush = mask;
            blob.AnchorPoint = new Vector2(0.5f, 0.5f);
            blob.Properties.InsertVector2("Start", startRel);
            blob.Properties.InsertVector2("End", endRel);
            blob.Properties.InsertVector2("SizeRel", sizeRel);
            blob.Properties.InsertScalar("Seconds", seconds);
            blob.Properties.InsertScalar("Delay", delay);
            _glowRoot!.Children.InsertAtTop(blob);

            var breathe = compositor.CreateVector3KeyFrameAnimation();
            breathe.InsertKeyFrame(0f, new Vector3(0.9f, 0.9f, 1f));
            breathe.InsertKeyFrame(0.5f, new Vector3(1.15f, 1.15f, 1f));
            breathe.InsertKeyFrame(1f, new Vector3(0.9f, 0.9f, 1f));
            breathe.Duration = TimeSpan.FromSeconds(seconds);
            breathe.IterationBehavior = AnimationIterationBehavior.Forever;
            breathe.DelayTime = TimeSpan.FromSeconds(delay);
            blob.StartAnimation("Scale", breathe);
        }

        // A bright, narrow diagonal band that periodically sweeps across the header and then pauses,
        // producing a Fluent "reveal" shimmer. Offset is driven in LayoutGlow (needs host size).
        private void AddSweep(Compositor compositor, Color color, float maxOpacity, float seconds, float delay, float angle)
        {
            byte core = (byte)(maxOpacity * 255);
            byte soft = (byte)(maxOpacity * 0.5f * 255);

            var band = compositor.CreateLinearGradientBrush();
            band.StartPoint = new Vector2(0f, 0.5f);
            band.EndPoint = new Vector2(1f, 0.5f);
            band.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(0.42f, Color.FromArgb(soft, color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(core, 255, 255, 255)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(0.58f, Color.FromArgb(soft, color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

            var mask = compositor.CreateMaskBrush();
            mask.Source = band;
            mask.Mask = VerticalFade(compositor);

            var sweep = compositor.CreateSpriteVisual();
            sweep.Brush = mask;
            sweep.AnchorPoint = new Vector2(0.5f, 0.5f);
            sweep.RotationAngleInDegrees = angle;
            sweep.Opacity = 0f;
            sweep.Properties.InsertScalar("Sweep", 1f);
            sweep.Properties.InsertScalar("Seconds", seconds);
            sweep.Properties.InsertScalar("Delay", delay);
            _glowRoot!.Children.InsertAtTop(sweep);

            // Flash the band on only while it crosses, then stay dark, with soft eased fade in/out.
            var flashEase = compositor.CreateCubicBezierEasingFunction(new Vector2(0.33f, 0f), new Vector2(0.67f, 1f));
            var flash = compositor.CreateScalarKeyFrameAnimation();
            flash.InsertKeyFrame(0f, 0f);
            flash.InsertKeyFrame(0.05f, 0f);
            flash.InsertKeyFrame(0.18f, maxOpacity, flashEase);
            flash.InsertKeyFrame(0.30f, maxOpacity);
            flash.InsertKeyFrame(0.42f, 0f, flashEase);
            flash.InsertKeyFrame(1f, 0f);
            flash.Duration = TimeSpan.FromSeconds(seconds);
            flash.IterationBehavior = AnimationIterationBehavior.Forever;
            flash.DelayTime = TimeSpan.FromSeconds(delay);
            sweep.StartAnimation("Opacity", flash);
        }

        // A soft, rotated band of light (transparent -> color -> transparent) that slowly drifts
        // and twinkles. Size/position are resolved against the host in LayoutGlow.
        private void AddStreak(Compositor compositor, Color color, Vector2 sizeRel, Vector2 startRel, Vector2 endRel, float maxOpacity, float minOpacity, int seconds, float delay, float angle)
        {
            var band = compositor.CreateLinearGradientBrush();
            band.StartPoint = new Vector2(0f, 0.5f);
            band.EndPoint = new Vector2(1f, 0.5f);
            band.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(0.25f, Color.FromArgb((byte)(maxOpacity * 0.35f * 255), color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb((byte)(maxOpacity * 255), color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(0.75f, Color.FromArgb((byte)(maxOpacity * 0.35f * 255), color.R, color.G, color.B)));
            band.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

            var mask = compositor.CreateMaskBrush();
            mask.Source = band;
            mask.Mask = VerticalFade(compositor);

            var streak = compositor.CreateSpriteVisual();
            streak.Brush = mask;
            streak.AnchorPoint = new Vector2(0.5f, 0.5f);
            streak.RotationAngleInDegrees = angle;
            streak.Opacity = minOpacity;
            streak.Properties.InsertVector2("Start", startRel);
            streak.Properties.InsertVector2("End", endRel);
            streak.Properties.InsertVector2("SizeRel", sizeRel);
            streak.Properties.InsertScalar("Seconds", seconds);
            streak.Properties.InsertScalar("Delay", delay);
            _glowRoot!.Children.InsertAtTop(streak);

            var twinkleEase = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0f), new Vector2(0.58f, 1f));
            var twinkle = compositor.CreateScalarKeyFrameAnimation();
            twinkle.InsertKeyFrame(0f, minOpacity);
            twinkle.InsertKeyFrame(0.5f, maxOpacity, twinkleEase);
            twinkle.InsertKeyFrame(1f, minOpacity, twinkleEase);
            twinkle.Duration = TimeSpan.FromSeconds(seconds * 0.6);
            twinkle.IterationBehavior = AnimationIterationBehavior.Forever;
            twinkle.DelayTime = TimeSpan.FromSeconds(delay);
            streak.StartAnimation("Opacity", twinkle);
        }

        // A tiny radial highlight that fades and pulses in place to read as a shimmer/sparkle.
        private void AddSparkle(Compositor compositor, Color color, Vector2 posRel, float maxOpacity, float diameter, float delay)
        {
            var radial = compositor.CreateRadialGradientBrush();
            radial.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(255, color.R, color.G, color.B)));
            radial.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

            var sparkle = compositor.CreateSpriteVisual();
            sparkle.Brush = radial;
            sparkle.Size = new Vector2(diameter, diameter);
            sparkle.AnchorPoint = new Vector2(0.5f, 0.5f);
            sparkle.CenterPoint = new Vector3(diameter / 2f, diameter / 2f, 0);
            sparkle.Opacity = 0f;
            sparkle.Properties.InsertVector2("Pos", posRel);
            _glowRoot!.Children.InsertAtTop(sparkle);

            var twinkle = compositor.CreateScalarKeyFrameAnimation();
            twinkle.InsertKeyFrame(0f, 0f);
            twinkle.InsertKeyFrame(0.5f, maxOpacity);
            twinkle.InsertKeyFrame(1f, 0f);
            twinkle.Duration = TimeSpan.FromSeconds(2.6);
            twinkle.IterationBehavior = AnimationIterationBehavior.Forever;
            twinkle.DelayTime = TimeSpan.FromSeconds(delay);
            sparkle.StartAnimation("Opacity", twinkle);

            var pulse = compositor.CreateVector3KeyFrameAnimation();
            pulse.InsertKeyFrame(0f, new Vector3(0.4f, 0.4f, 1f));
            pulse.InsertKeyFrame(0.5f, new Vector3(1f, 1f, 1f));
            pulse.InsertKeyFrame(1f, new Vector3(0.4f, 0.4f, 1f));
            pulse.Duration = TimeSpan.FromSeconds(2.6);
            pulse.IterationBehavior = AnimationIterationBehavior.Forever;
            pulse.DelayTime = TimeSpan.FromSeconds(delay);
            sparkle.StartAnimation("Scale", pulse);
        }

        private void LayoutGlow()
        {
            if (_glowRoot == null)
            {
                return;
            }

            var size = new Vector2((float)HeaderGlowHost.ActualWidth, (float)HeaderGlowHost.ActualHeight);
            if (size.X <= 0 || size.Y <= 0)
            {
                return;
            }

            Compositor compositor = _glowRoot.Compositor;
            _glowRoot.Size = size;
            _glowRoot.Clip = compositor.CreateInsetClip();

            foreach (var child in _glowRoot.Children)
            {
                if (child is not SpriteVisual visual)
                {
                    continue;
                }

                // Sparkles: fixed-size dots anchored at a relative point.
                if (visual.Properties.TryGetVector2("Pos", out Vector2 pos) == CompositionGetValueStatus.Succeeded)
                {
                    visual.Offset = new Vector3(pos.X * size.X, pos.Y * size.Y, 0);
                    continue;
                }

                // Shine sweep: narrow band that races across in the first third of the cycle, then holds.
                if (visual.Properties.TryGetScalar("Sweep", out float sweepFlag) == CompositionGetValueStatus.Succeeded && sweepFlag > 0f)
                {
                    visual.Size = new Vector2(size.X * 0.5f, size.Y * 1.9f);
                    visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0);

                    visual.Properties.TryGetScalar("Seconds", out float sweepSecs);
                    visual.Properties.TryGetScalar("Delay", out float sweepDelay);

                    float midY = size.Y * 0.5f;
                    var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.35f, 1f));
                    var run = compositor.CreateVector3KeyFrameAnimation();
                    run.InsertKeyFrame(0f, new Vector3(-0.45f * size.X, midY, 0));
                    run.InsertKeyFrame(0.42f, new Vector3(1.45f * size.X, midY, 0), ease);
                    run.InsertKeyFrame(1f, new Vector3(1.45f * size.X, midY, 0));
                    run.Duration = TimeSpan.FromSeconds(sweepSecs > 0 ? sweepSecs : 5);
                    run.DelayTime = TimeSpan.FromSeconds(sweepDelay);
                    run.IterationBehavior = AnimationIterationBehavior.Forever;
                    visual.Offset = new Vector3(-0.45f * size.X, midY, 0);
                    visual.StartAnimation("Offset", run);
                    continue;
                }

                // Aurora clouds & streaks: size relative to the host, rotate about their center,
                // and gently drift back and forth between two points.
                if (visual.Properties.TryGetVector2("SizeRel", out Vector2 sizeRel) == CompositionGetValueStatus.Succeeded
                    && visual.Properties.TryGetVector2("Start", out Vector2 start) == CompositionGetValueStatus.Succeeded
                    && visual.Properties.TryGetVector2("End", out Vector2 end) == CompositionGetValueStatus.Succeeded)
                {
                    visual.Size = new Vector2(sizeRel.X * size.X, sizeRel.Y * size.Y);
                    visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0);

                    visual.Properties.TryGetScalar("Seconds", out float secs);
                    visual.Properties.TryGetScalar("Delay", out float delay);

                    var driftEase = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0f), new Vector2(0.58f, 1f));
                    var drift = compositor.CreateVector3KeyFrameAnimation();
                    drift.InsertKeyFrame(0f, new Vector3(start.X * size.X, start.Y * size.Y, 0));
                    drift.InsertKeyFrame(1f, new Vector3(end.X * size.X, end.Y * size.Y, 0), driftEase);
                    drift.Duration = TimeSpan.FromSeconds(secs > 0 ? secs : 20);
                    drift.DelayTime = TimeSpan.FromSeconds(delay);
                    drift.IterationBehavior = AnimationIterationBehavior.Forever;
                    drift.Direction = Microsoft.UI.Composition.AnimationDirection.Alternate;
                    visual.Offset = new Vector3(start.X * size.X, start.Y * size.Y, 0);
                    visual.StartAnimation("Offset", drift);
                }
            }
        }
    }
}

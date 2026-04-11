// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class IconCarouselControl : UserControl
{
    private const int SlotCount = 7;
    private const float CardSize = 108f;
    private const float CardCornerRadius = 20f;
    private const float IconPadding = 14f;
    private const float Stride = CardSize * 0.50f;

    private static readonly TimeSpan RotateInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(250);

    private static readonly SlotLayout[] Slots =
    [
        new(-3, 0.55f, 0.7f),
        new(-2, 0.65f, 0.8f),
        new(-1, 0.80f, 0.9f),
        new(0, 1.00f, 1.0f),
        new(1, 0.80f, 0.9f),
        new(2, 0.65f, 0.8f),
        new(3, 0.55f, 0.7f),
    ];

    public static readonly DependencyProperty IconUrisProperty =
        DependencyProperty.Register(
            nameof(IconUris),
            typeof(IReadOnlyList<Uri>),
            typeof(IconCarouselControl),
            new PropertyMetadata(null, OnIconUrisChanged));

    private readonly List<ContainerVisual> _cards = [];
    private readonly List<LoadedImageSurface> _surfaces = [];
    private readonly List<CompositionColorBrush> _cardFillBrushes = [];
    private readonly List<CompositionColorBrush> _cardStrokeBrushes = [];
    private Compositor? _compositor;
    private ContainerVisual? _container;
    private DispatcherTimer? _timer;
    private int _rotationIndex;

    public IconCarouselControl()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.SizeChanged += OnSizeChanged;
        this.ActualThemeChanged += OnActualThemeChanged;
    }

    public IReadOnlyList<Uri>? IconUris
    {
        get => (IReadOnlyList<Uri>?)GetValue(IconUrisProperty);
        set => SetValue(IconUrisProperty, value);
    }

    private static void OnIconUrisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconCarouselControl control)
        {
            control.RebuildVisuals();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hostVisual = ElementCompositionPreview.GetElementVisual(RootGrid);
        _compositor = hostVisual.Compositor;

        _container = _compositor.CreateContainerVisual();
        _container.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        ElementCompositionPreview.SetElementChildVisual(RootGrid, _container);

        RebuildVisuals();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopTimer();
        CleanupVisuals();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_container != null)
        {
            _container.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }

        SnapAllToCurrentSlots();
    }

    private void RebuildVisuals()
    {
        if (_compositor == null || _container == null)
        {
            return;
        }

        StopTimer();
        CleanupVisuals();

        var uris = IconUris;
        if (uris == null || uris.Count == 0)
        {
            return;
        }

        _rotationIndex = 0;

        for (var i = 0; i < uris.Count; i++)
        {
            var card = CreateIconCard(uris[i]);
            _cards.Add(card);
        }

        _container.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        SnapAllToCurrentSlots();
        ApplyZOrder();
        StartTimer();
    }

    private ContainerVisual CreateIconCard(Uri iconUri)
    {
        // Oversized container so shadow blur isn't clipped
        var shadowPad = 24f;
        var containerSize = CardSize + (shadowPad * 2);

        var card = _compositor!.CreateContainerVisual();
        card.Size = new Vector2(containerSize, containerSize);
        card.AnchorPoint = new Vector2(0.5f, 0.5f);

        // Create a rounded-rect shape to use as shadow mask
        var maskGeometry = _compositor.CreateRoundedRectangleGeometry();
        maskGeometry.Size = new Vector2(CardSize, CardSize);
        maskGeometry.CornerRadius = new Vector2(CardCornerRadius, CardCornerRadius);

        var maskShape = _compositor.CreateSpriteShape(maskGeometry);
        maskShape.FillBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 255, 255, 255));

        var maskShapeVisual = _compositor.CreateShapeVisual();
        maskShapeVisual.Size = new Vector2(CardSize, CardSize);
        maskShapeVisual.Shapes.Add(maskShape);

        var maskSurface = _compositor.CreateVisualSurface();
        maskSurface.SourceVisual = maskShapeVisual;
        maskSurface.SourceSize = new Vector2(CardSize, CardSize);

        var maskBrush = _compositor.CreateSurfaceBrush(maskSurface);

        // Shadow visual: card-sized, centered in the oversized container
        var shadow = _compositor.CreateDropShadow();
        shadow.BlurRadius = 8f;
        shadow.Offset = new Vector3(0, 3f, 0);
        shadow.Color = Color.FromArgb(30, 0, 0, 0);
        shadow.Mask = maskBrush;

        var shadowVisual = _compositor.CreateSpriteVisual();
        shadowVisual.Size = new Vector2(CardSize, CardSize);
        shadowVisual.Offset = new Vector3(shadowPad, shadowPad, 0);
        shadowVisual.Shadow = shadow;
        card.Children.InsertAtBottom(shadowVisual);

        // Rounded card background with soft stroke
        var bgGeometry = _compositor.CreateRoundedRectangleGeometry();
        bgGeometry.Size = new Vector2(CardSize, CardSize);
        bgGeometry.Offset = new Vector2(shadowPad, shadowPad);
        bgGeometry.CornerRadius = new Vector2(CardCornerRadius, CardCornerRadius);

        var fillBrush = _compositor.CreateColorBrush(GetCardBackgroundColor());
        var strokeBrush = _compositor.CreateColorBrush(GetCardStrokeColor());
        _cardFillBrushes.Add(fillBrush);
        _cardStrokeBrushes.Add(strokeBrush);

        var bgShape = _compositor.CreateSpriteShape(bgGeometry);
        bgShape.FillBrush = fillBrush;
        bgShape.StrokeBrush = strokeBrush;
        bgShape.StrokeThickness = 1f;

        var bgVisual = _compositor.CreateShapeVisual();
        bgVisual.Size = new Vector2(containerSize, containerSize);
        bgVisual.Shapes.Add(bgShape);
        card.Children.InsertAbove(bgVisual, shadowVisual);

        // Icon image
        var surface = LoadedImageSurface.StartLoadFromUri(iconUri);
        _surfaces.Add(surface);

        var surfaceBrush = _compositor.CreateSurfaceBrush(surface);
        surfaceBrush.Stretch = CompositionStretch.Uniform;
        surfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.Linear;

        var iconSize = CardSize - (IconPadding * 2);
        var iconVisual = _compositor.CreateSpriteVisual();
        iconVisual.Size = new Vector2(iconSize, iconSize);
        iconVisual.Offset = new Vector3(shadowPad + IconPadding, shadowPad + IconPadding, 0);
        iconVisual.Brush = surfaceBrush;

        var iconClipGeometry = _compositor.CreateRoundedRectangleGeometry();
        iconClipGeometry.Size = new Vector2(iconSize, iconSize);
        iconClipGeometry.CornerRadius = new Vector2(CardCornerRadius - 4, CardCornerRadius - 4);
        iconVisual.Clip = _compositor.CreateGeometricClip(iconClipGeometry);

        card.Children.InsertAtTop(iconVisual);

        return card;
    }

    private Vector3 GetSlotOffset(SlotLayout slot)
    {
        var centerX = (float)ActualWidth / 2f;
        var centerY = (float)ActualHeight / 2f;
        return new Vector3(centerX + (slot.PositionFromCenter * Stride), centerY, 0);
    }

    private void SnapAllToCurrentSlots()
    {
        if (_cards.Count == 0 || ActualWidth < 1)
        {
            return;
        }

        for (var i = 0; i < _cards.Count; i++)
        {
            var slotIndex = GetSlotForCard(i);
            var card = _cards[i];

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                card.IsVisible = false;
                continue;
            }

            card.IsVisible = true;
            var slot = Slots[slotIndex];
            card.Offset = GetSlotOffset(slot);
            card.Scale = new Vector3(slot.Scale, slot.Scale, 1f);
            card.Opacity = slot.Opacity;
        }
    }

    private void RotateForward()
    {
        if (_cards.Count == 0 || _compositor == null || ActualWidth < 1)
        {
            return;
        }

        // Find the card that is about to depart (currently at slot 0, will go to slot -1)
        // and the card about to arrive (currently at slot SlotCount, will enter slot SlotCount-1)
        var departingCardIndex = -1;
        var arrivingCardIndex = -1;
        for (var i = 0; i < _cards.Count; i++)
        {
            var slot = GetSlotForCard(i);
            if (slot == 0)
            {
                departingCardIndex = i;
            }
            else if (slot == SlotCount)
            {
                arrivingCardIndex = i;
            }
        }

        // Advance rotation
        _rotationIndex = (_rotationIndex + 1) % _cards.Count;

        // Set z-order BEFORE animating. The departing card goes to the back,
        // the arriving card also starts at the back. Since they're behind
        // everything, the z-order change is invisible.
        ApplyZOrder();

        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1f));

        // Animate all currently visible cards to their new slots
        for (var i = 0; i < _cards.Count; i++)
        {
            var slotIndex = GetSlotForCard(i);
            var card = _cards[i];

            if (i == departingCardIndex)
            {
                // Departing: animate off-screen left, then teleport to right
                AnimateDeparture(card, easing);
                continue;
            }

            if (i == arrivingCardIndex)
            {
                // Arriving: teleport to right of visible area, animate slide-in
                AnimateArrival(card, slotIndex, easing);
                continue;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                card.IsVisible = false;
                continue;
            }

            // Normal cards: animate to new position
            card.IsVisible = true;
            var slot = Slots[slotIndex];
            AnimateCardTo(card, GetSlotOffset(slot), slot.Scale, slot.Opacity, easing);
        }
    }

    private void AnimateDeparture(ContainerVisual card, CompositionEasingFunction easing)
    {
        // Slide off to the left and fade out
        var leftEdge = Slots[0];
        var departTarget = GetSlotOffset(leftEdge) - new Vector3(Stride * 1.5f, 0, 0);

        var offsetAnim = _compositor!.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(1f, departTarget, easing);
        offsetAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Offset), offsetAnim);

        var opacityAnim = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1f, 0f, easing);
        opacityAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Opacity), opacityAnim);

        var scaleVal = leftEdge.Scale * 0.8f;
        var scaleAnim = _compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(1f, new Vector3(scaleVal, scaleVal, 1f), easing);
        scaleAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Scale), scaleAnim);
    }

    private void AnimateArrival(ContainerVisual card, int slotIndex, CompositionEasingFunction easing)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            card.IsVisible = false;
            return;
        }

        var slot = Slots[slotIndex];
        var targetOffset = GetSlotOffset(slot);

        // Start off-screen to the right
        card.StopAnimation(nameof(card.Offset));
        card.StopAnimation(nameof(card.Scale));
        card.StopAnimation(nameof(card.Opacity));
        card.Offset = targetOffset + new Vector3(Stride * 1.5f, 0, 0);
        card.Scale = new Vector3(slot.Scale * 0.8f, slot.Scale * 0.8f, 1f);
        card.Opacity = 0f;
        card.IsVisible = true;

        // Animate slide-in from the right
        AnimateCardTo(card, targetOffset, slot.Scale, slot.Opacity, easing);
    }

    private void AnimateCardTo(ContainerVisual card, Vector3 offset, float scale, float opacity, CompositionEasingFunction easing)
    {
        var offsetAnim = _compositor!.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(1f, offset, easing);
        offsetAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Offset), offsetAnim);

        var scaleAnim = _compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(1f, new Vector3(scale, scale, 1f), easing);
        scaleAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Scale), scaleAnim);

        var opacityAnim = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1f, opacity, easing);
        opacityAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Opacity), opacityAnim);
    }

    private void ApplyZOrder()
    {
        if (_container == null)
        {
            return;
        }

        var centerSlot = SlotCount / 2;

        _container.Children.RemoveAll();
        var ordered = new List<(int DistFromCenter, ContainerVisual Card)>();
        for (var i = 0; i < _cards.Count; i++)
        {
            var slotIndex = GetSlotForCard(i);
            if (slotIndex >= 0 && slotIndex < SlotCount)
            {
                ordered.Add((Math.Abs(slotIndex - centerSlot), _cards[i]));
            }
        }

        // Farthest from center first (bottom), closest last (top)
        ordered.Sort((a, b) => b.DistFromCenter.CompareTo(a.DistFromCenter));
        foreach (var item in ordered)
        {
            _container.Children.InsertAtTop(item.Card);
        }
    }

    private int GetSlotForCard(int cardIndex)
    {
        var totalCards = _cards.Count;
        var centerSlot = SlotCount / 2;
        var diff = cardIndex - _rotationIndex;

        if (diff > totalCards / 2)
        {
            diff -= totalCards;
        }
        else if (diff < -(totalCards / 2))
        {
            diff += totalCards;
        }

        return centerSlot + diff;
    }

    private void StartTimer()
    {
        if (_timer != null || _cards.Count <= SlotCount)
        {
            return;
        }

        _timer = new DispatcherTimer
        {
            Interval = RotateInterval,
        };
        _timer.Tick += OnRotateTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnRotateTick;
            _timer = null;
        }
    }

    private void OnRotateTick(object? sender, object e)
    {
        RotateForward();
    }

    private Color GetCardBackgroundColor()
    {
        return ResolveThemeColor("SolidBackgroundFillColorTertiaryBrush", Color.FromArgb(200, 40, 40, 40));
    }

    private Color GetCardStrokeColor()
    {
        return ResolveThemeColor("CardStrokeColorDefaultBrush", Color.FromArgb(20, 0, 0, 0));
    }

    private Color ResolveThemeColor(string resourceKey, Color fallback)
    {
        if (Resources.TryGetValue(resourceKey, out var res) && res is SolidColorBrush brush)
        {
            return brush.Color;
        }

        if (Application.Current.Resources.TryGetValue(resourceKey, out res) && res is SolidColorBrush appBrush)
        {
            return appBrush.Color;
        }

        return fallback;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        var fillColor = GetCardBackgroundColor();
        var strokeColor = GetCardStrokeColor();

        foreach (var brush in _cardFillBrushes)
        {
            brush.Color = fillColor;
        }

        foreach (var brush in _cardStrokeBrushes)
        {
            brush.Color = strokeColor;
        }
    }

    private void CleanupVisuals()
    {
        foreach (var card in _cards)
        {
            card.Dispose();
        }

        _cards.Clear();
        _cardFillBrushes.Clear();
        _cardStrokeBrushes.Clear();

        foreach (var surface in _surfaces)
        {
            surface.Dispose();
        }

        _surfaces.Clear();

        if (_container != null)
        {
            _container.Children.RemoveAll();
        }
    }

    private readonly record struct SlotLayout(int PositionFromCenter, float Scale, float Opacity);
}

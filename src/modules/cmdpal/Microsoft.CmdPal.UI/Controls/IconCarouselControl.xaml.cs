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
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class IconCarouselControl : UserControl
{
    private const int SlotCount = 7;
    private const int TransitionSlotBeforeFirst = -1;
    private const int TransitionSlotAfterLast = SlotCount;
    private const int HiddenSlot = int.MinValue;
    private const float CardSize = 108f;
    private const float CardCornerRadius = 20f;
    private const float IconPadding = 14f;
    private const float Stride = CardSize * 0.50f;
    private const float IconDecodeScaleFactor = 1.5f;
    private const float TransitionScaleFactor = 0.92f;
    private const float TransitionOpacityFactor = 0.20f;
    private const float ArrivalFadeHoldProgress = 0.40f;
    private const float ArrivalFadeMidpointProgress = 0.78f;
    private const float ArrivalFadeMidpointFactor = 0.72f;

    private static readonly TimeSpan RotateInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(400);

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
    private int[] _displaySlots = [];
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

    public event TypedEventHandler<IconCarouselControl, IconCarouselItemInvokedEventArgs>? ItemInvoked;

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
            _displaySlots = [];
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

        var iconSize = CardSize - (IconPadding * 2);

        // Request a slightly larger surface so the smaller carousel slots can
        // downsample from a cleaner source instead of revealing raster edges.
        var surface = LoadedImageSurface.StartLoadFromUri(iconUri, GetDesiredIconSurfaceSize(iconSize));
        _surfaces.Add(surface);

        var surfaceBrush = _compositor.CreateSurfaceBrush(surface);
        surfaceBrush.Stretch = CompositionStretch.Uniform;
        surfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.MagLinearMinLinearMipLinear;
        surfaceBrush.HorizontalAlignmentRatio = 0.5f;
        surfaceBrush.VerticalAlignmentRatio = 0.5f;

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

        var previousRotationIndex = _rotationIndex;
        var nextRotationIndex = (previousRotationIndex + 1) % _cards.Count;
        var displaySlots = new int[_cards.Count];

        // Prep start states before reordering children so edge cards can glide in/out
        // instead of popping as they cross the visible bounds.
        for (var i = 0; i < _cards.Count; i++)
        {
            var currentSlot = GetSlotForCard(i, previousRotationIndex);
            var nextSlot = GetSlotForCard(i, nextRotationIndex);
            var card = _cards[i];

            displaySlots[i] = GetDisplaySlot(currentSlot, nextSlot);

            if (!IsVisibleSlot(currentSlot) && IsVisibleSlot(nextSlot))
            {
                PrepareArrival(card, nextSlot, currentSlot < 0);
            }
            else if (!IsVisibleSlot(currentSlot) && !IsVisibleSlot(nextSlot))
            {
                card.IsVisible = false;
            }
            else
            {
                card.IsVisible = true;
            }
        }

        _rotationIndex = nextRotationIndex;

        // Set z-order BEFORE animating. The departing card goes to the back,
        // the arriving card also starts at the back. Since they're behind
        // everything, the z-order change is invisible.
        ApplyZOrder(displaySlots);

        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.37f, 0f), new Vector2(0.63f, 1f));

        for (var i = 0; i < _cards.Count; i++)
        {
            var currentSlot = GetSlotForCard(i, previousRotationIndex);
            var nextSlot = GetSlotForCard(i, nextRotationIndex);
            var card = _cards[i];

            if (IsVisibleSlot(currentSlot) && !IsVisibleSlot(nextSlot))
            {
                AnimateDeparture(card, nextSlot < 0, easing);
                continue;
            }

            if (!IsVisibleSlot(currentSlot) && IsVisibleSlot(nextSlot))
            {
                AnimateArrival(card, nextSlot, easing);
                continue;
            }

            if (!IsVisibleSlot(nextSlot))
            {
                card.IsVisible = false;
                continue;
            }

            var slot = Slots[nextSlot];
            AnimateCardTo(card, GetSlotOffset(slot), slot.Scale, slot.Opacity, easing);
        }
    }

    private void AnimateDeparture(ContainerVisual card, bool exitingLeft, CompositionEasingFunction easing)
    {
        var edgeSlot = exitingLeft ? Slots[0] : Slots[^1];
        var targetScale = edgeSlot.Scale * TransitionScaleFactor;
        AnimateCardTo(card, GetTransitionOffset(exitingLeft), targetScale, 0f, easing);
    }

    private void AnimateArrival(ContainerVisual card, int slotIndex, CompositionEasingFunction easing)
    {
        if (!IsVisibleSlot(slotIndex))
        {
            card.IsVisible = false;
            return;
        }

        var slot = Slots[slotIndex];
        StartOffsetAnimation(card, GetSlotOffset(slot), easing);
        StartScaleAnimation(card, slot.Scale, easing);
        StartArrivalOpacityAnimation(card, slot.Opacity, easing);
    }

    private void AnimateCardTo(ContainerVisual card, Vector3 offset, float scale, float opacity, CompositionEasingFunction easing)
    {
        StartOffsetAnimation(card, offset, easing);
        StartScaleAnimation(card, scale, easing);
        StartOpacityAnimation(card, opacity, easing);
    }

    private void StartOffsetAnimation(ContainerVisual card, Vector3 offset, CompositionEasingFunction easing)
    {
        var offsetAnim = _compositor!.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(1f, offset, easing);
        offsetAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Offset), offsetAnim);
    }

    private void StartScaleAnimation(ContainerVisual card, float scale, CompositionEasingFunction easing)
    {
        var scaleAnim = _compositor!.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(1f, new Vector3(scale, scale, 1f), easing);
        scaleAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Scale), scaleAnim);
    }

    private void StartOpacityAnimation(ContainerVisual card, float opacity, CompositionEasingFunction easing)
    {
        var opacityAnim = _compositor!.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1f, opacity, easing);
        opacityAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Opacity), opacityAnim);
    }

    private void StartArrivalOpacityAnimation(ContainerVisual card, float targetOpacity, CompositionEasingFunction easing)
    {
        var startOpacity = targetOpacity * TransitionOpacityFactor;
        var midpointOpacity = MathF.Max(startOpacity, targetOpacity * ArrivalFadeMidpointFactor);

        var opacityAnim = _compositor!.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(ArrivalFadeHoldProgress, startOpacity);
        opacityAnim.InsertKeyFrame(ArrivalFadeMidpointProgress, midpointOpacity, easing);
        opacityAnim.InsertKeyFrame(1f, targetOpacity, easing);
        opacityAnim.Duration = AnimationDuration;
        card.StartAnimation(nameof(card.Opacity), opacityAnim);
    }

    private void ApplyZOrder()
    {
        var displaySlots = new int[_cards.Count];
        for (var i = 0; i < _cards.Count; i++)
        {
            var slotIndex = GetSlotForCard(i);
            displaySlots[i] = IsVisibleSlot(slotIndex) ? slotIndex : HiddenSlot;
        }

        ApplyZOrder(displaySlots);
    }

    private void ApplyZOrder(IReadOnlyList<int> displaySlots)
    {
        _displaySlots = [.. displaySlots];

        if (_container == null)
        {
            return;
        }

        var centerSlot = SlotCount / 2;

        _container.Children.RemoveAll();
        var ordered = new List<(int DistFromCenter, int SlotIndex, ContainerVisual Card)>();
        for (var i = 0; i < _cards.Count; i++)
        {
            var slotIndex = displaySlots[i];
            if (slotIndex != HiddenSlot)
            {
                ordered.Add((Math.Abs(slotIndex - centerSlot), slotIndex, _cards[i]));
            }
        }

        // Farthest from center first (bottom), closest last (top)
        ordered.Sort((a, b) =>
        {
            var distanceOrder = b.DistFromCenter.CompareTo(a.DistFromCenter);
            return distanceOrder != 0 ? distanceOrder : a.SlotIndex.CompareTo(b.SlotIndex);
        });

        foreach (var item in ordered)
        {
            _container.Children.InsertAtTop(item.Card);
        }
    }

    private int GetSlotForCard(int cardIndex)
    {
        return GetSlotForCard(cardIndex, _rotationIndex);
    }

    private int GetSlotForCard(int cardIndex, int rotationIndex)
    {
        var totalCards = _cards.Count;
        var centerSlot = SlotCount / 2;
        var diff = cardIndex - rotationIndex;

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

    private static bool IsVisibleSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    private static int GetDisplaySlot(int currentSlot, int nextSlot)
    {
        if (IsVisibleSlot(nextSlot))
        {
            return IsVisibleSlot(currentSlot) ? nextSlot : (currentSlot < 0 ? TransitionSlotBeforeFirst : TransitionSlotAfterLast);
        }

        if (IsVisibleSlot(currentSlot))
        {
            return nextSlot < 0 ? TransitionSlotBeforeFirst : TransitionSlotAfterLast;
        }

        return HiddenSlot;
    }

    private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var uris = IconUris;
        if (uris is null || uris.Count == 0)
        {
            return;
        }

        var hitCardIndex = HitTestCardIndex(e.GetPosition(RootGrid));
        if (hitCardIndex < 0 || hitCardIndex >= uris.Count)
        {
            return;
        }

        ItemInvoked?.Invoke(this, new IconCarouselItemInvokedEventArgs(hitCardIndex, uris[hitCardIndex]));
        e.Handled = true;
    }

    private int HitTestCardIndex(Point point)
    {
        if (_cards.Count == 0 || _displaySlots.Length != _cards.Count)
        {
            return -1;
        }

        var centerSlot = SlotCount / 2;
        List<(int DistFromCenter, int SlotIndex, int CardIndex)> ordered = [];

        for (var i = 0; i < _cards.Count; i++)
        {
            var slotIndex = _displaySlots[i];
            if (slotIndex == HiddenSlot || !_cards[i].IsVisible)
            {
                continue;
            }

            ordered.Add((Math.Abs(slotIndex - centerSlot), slotIndex, i));
        }

        ordered.Sort((a, b) =>
        {
            var distanceOrder = a.DistFromCenter.CompareTo(b.DistFromCenter);
            return distanceOrder != 0 ? distanceOrder : b.SlotIndex.CompareTo(a.SlotIndex);
        });

        foreach (var item in ordered)
        {
            if (IsPointOverCard(_cards[item.CardIndex], point))
            {
                return item.CardIndex;
            }
        }

        return -1;
    }

    private static bool IsPointOverCard(ContainerVisual card, Point point)
    {
        var halfExtent = (CardSize * card.Scale.X) / 2f;
        return point.X >= card.Offset.X - halfExtent
            && point.X <= card.Offset.X + halfExtent
            && point.Y >= card.Offset.Y - halfExtent
            && point.Y <= card.Offset.Y + halfExtent;
    }

    private void PrepareArrival(ContainerVisual card, int slotIndex, bool enteringFromLeft)
    {
        var slot = Slots[slotIndex];
        var startScale = slot.Scale * TransitionScaleFactor;

        card.StopAnimation(nameof(card.Offset));
        card.StopAnimation(nameof(card.Scale));
        card.StopAnimation(nameof(card.Opacity));
        card.Offset = GetTransitionOffset(enteringFromLeft);
        card.Scale = new Vector3(startScale, startScale, 1f);
        card.Opacity = slot.Opacity * TransitionOpacityFactor;
        card.IsVisible = true;
    }

    private Vector3 GetTransitionOffset(bool toLeft)
    {
        var edgeSlot = toLeft ? Slots[0] : Slots[^1];
        var direction = toLeft ? -1f : 1f;
        return GetSlotOffset(edgeSlot) + new Vector3(direction * Stride, 0f, 0f);
    }

    private static global::Windows.Foundation.Size GetDesiredIconSurfaceSize(float iconSize)
    {
        var desiredSize = Math.Ceiling(iconSize * IconDecodeScaleFactor);
        return new global::Windows.Foundation.Size(desiredSize, desiredSize);
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
        _displaySlots = [];
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

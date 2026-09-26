// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using ManagedCommon;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Settings;

internal sealed class SettingsTargetHighlighter
{
    private const float GlowBlurRadius = 16f;
    private const float GlowOpacity = 0.3f;
    private const float HighContrastGlowOpacity = 0.8f;
    private const float HighlightPadding = 4f;

    // Marks our glow visual. GetElementChildVisual may return a different projection
    // wrapper for the same visual, so ownership is checked by tag, not by reference.
    private const string HighlightVisualComment = "CmdPal.SettingsLinkHighlight";
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromMilliseconds(1986);

    private readonly AccessibilitySettings _accessibilitySettings = new();
    private readonly UISettings _uiSettings = new();
    private ActiveHighlight? _activeHighlight;
    private bool _isClosed;

    internal void Highlight(FrameworkElement target, bool animationsEnabled)
    {
        Clear();
        if (_isClosed || target.ActualWidth <= 0 || target.ActualHeight <= 0)
        {
            return;
        }

        try
        {
            // Preserve controls that already own a composition child visual; a leftover
            // glow of ours is replaced instead.
            if (ElementCompositionPreview.GetElementChildVisual(target) is { } existing &&
                !IsHighlightVisual(existing))
            {
                return;
            }

            var compositor = ElementCompositionPreview.GetElementVisual(target).Compositor;
            var highContrast = _accessibilitySettings.HighContrast;
            var dropShadow = compositor.CreateDropShadow();
            dropShadow.BlurRadius = GlowBlurRadius;
            dropShadow.Color = GetHighlightColor(highContrast);
            dropShadow.Offset = Vector3.Zero;
            dropShadow.Opacity = 0f;

            var spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Comment = HighlightVisualComment;
            spriteVisual.Offset = new Vector3(-HighlightPadding, -HighlightPadding, 0);
            spriteVisual.Shadow = dropShadow;
            spriteVisual.Size = new Vector2(
                (float)target.ActualWidth + (HighlightPadding * 2),
                (float)target.ActualHeight + (HighlightPadding * 2));

            var timer = target.DispatcherQueue.CreateTimer();
            timer.Interval = HighlightDuration;

            var highlight = new ActiveHighlight(this, target, spriteVisual, timer);
            _activeHighlight = highlight;
            ElementCompositionPreview.SetElementChildVisual(target, spriteVisual);

            if (animationsEnabled && _uiSettings.AnimationsEnabled && !highContrast)
            {
                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0f, 0f);
                animation.InsertKeyFrame(0.5f, GlowOpacity);
                animation.InsertKeyFrame(1f, 0f);
                animation.Duration = HighlightDuration;
                dropShadow.StartAnimation(nameof(dropShadow.Opacity), animation);
            }
            else
            {
                dropShadow.Opacity = highContrast ? HighContrastGlowOpacity : GlowOpacity;
            }

            highlight.Start();
        }
        catch (Exception ex)
        {
            Clear();
            Logger.LogError("Failed to highlight a settings link target.", ex);
        }
    }

    internal void Clear()
    {
        var highlight = _activeHighlight;
        _activeHighlight = null;
        highlight?.Clear();
    }

    internal void Close()
    {
        if (_isClosed)
        {
            return;
        }

        Clear();
        _isClosed = true;
    }

    private static bool IsHighlightVisual(Visual visual) =>
        string.Equals(visual.Comment, HighlightVisualComment, StringComparison.Ordinal);

    private static Color GetHighlightColor(bool highContrast) =>
        (Color)Application.Current.Resources[
            highContrast ? "SystemColorHighlightColor" : "SystemAccentColorLight2"];

    private void Complete(ActiveHighlight highlight)
    {
        if (!ReferenceEquals(_activeHighlight, highlight))
        {
            return;
        }

        _activeHighlight = null;
        highlight.Clear();
    }

    private sealed class ActiveHighlight
    {
        private readonly SettingsTargetHighlighter _owner;
        private readonly FrameworkElement _target;
        private readonly SpriteVisual _visual;
        private readonly DispatcherQueueTimer _timer;
        private bool _isCleared;

        internal ActiveHighlight(
            SettingsTargetHighlighter owner,
            FrameworkElement target,
            SpriteVisual visual,
            DispatcherQueueTimer timer)
        {
            _owner = owner;
            _target = target;
            _visual = visual;
            _timer = timer;
            _timer.Tick += Timer_Tick;
            _target.Unloaded += Target_Unloaded;
        }

        internal void Start() => _timer.Start();

        internal void Clear()
        {
            if (_isCleared)
            {
                return;
            }

            _isCleared = true;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _target.Unloaded -= Target_Unloaded;

            try
            {
                // Hide through our own reference first so the glow disappears even if
                // detaching below does not find it.
                _visual.IsVisible = false;
                if (ElementCompositionPreview.GetElementChildVisual(_target) is { } current &&
                    IsHighlightVisual(current))
                {
                    ElementCompositionPreview.SetElementChildVisual(_target, null);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear a settings link target highlight.", ex);
            }
        }

        private void Timer_Tick(DispatcherQueueTimer sender, object args) => _owner.Complete(this);

        private void Target_Unloaded(object sender, RoutedEventArgs args) => _owner.Complete(this);
    }
}

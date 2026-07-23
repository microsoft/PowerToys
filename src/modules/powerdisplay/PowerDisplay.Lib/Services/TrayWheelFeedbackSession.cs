// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Tracks ordinary-hover and adjustment-feedback presentation timing.
/// </summary>
public sealed class TrayWheelFeedbackSession
{
    /// <summary>
    /// Identifies the presentation requested by the current session state.
    /// </summary>
    public enum PresentationKind
    {
        Hidden,
        AppName,
        Adjustment,
    }

    /// <summary>
    /// Describes the presentation requested by one state transition.
    /// </summary>
    public readonly record struct Presentation(PresentationKind Kind, string? Text = null);

    private const long HoverDelayMilliseconds = 500;
    private const long AdjustmentDurationMilliseconds = 2000;

    private bool _isHovering;
    private long _hoverStartedAt;
    private string? _adjustmentText;
    private long _adjustmentStartedAt;

    /// <summary>
    /// Gets a value indicating whether a pointer hover session is active.
    /// </summary>
    public bool IsHovering => _isHovering;

    /// <summary>
    /// Starts or continues a hover session.
    /// </summary>
    public Presentation StartHover(long now)
    {
        if (!_isHovering)
        {
            _isHovering = true;
            _hoverStartedAt = now;
        }

        return Tick(now, pointerInside: true);
    }

    /// <summary>
    /// Shows adjustment text immediately and restarts its expiration deadline.
    /// </summary>
    public Presentation ShowAdjustment(string text, long now)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!_isHovering)
        {
            _isHovering = true;
            _hoverStartedAt = now;
        }

        _adjustmentText = text;
        _adjustmentStartedAt = now;
        return new Presentation(PresentationKind.Adjustment, text);
    }

    /// <summary>
    /// Clears adjustment text and returns the appropriate ordinary-hover state.
    /// </summary>
    public Presentation ClearAdjustment(long now, bool pointerInside)
    {
        _adjustmentText = null;
        _adjustmentStartedAt = 0;

        if (!pointerInside)
        {
            return Stop();
        }

        _isHovering = true;
        _hoverStartedAt = unchecked(now - HoverDelayMilliseconds);
        return new Presentation(PresentationKind.AppName);
    }

    /// <summary>
    /// Advances presentation state for the current pointer and timestamp.
    /// </summary>
    public Presentation Tick(long now, bool pointerInside)
    {
        if (!_isHovering || !pointerInside)
        {
            return Stop();
        }

        if (_adjustmentText is not null)
        {
            if (Elapsed(now, _adjustmentStartedAt) < AdjustmentDurationMilliseconds)
            {
                return new Presentation(PresentationKind.Adjustment, _adjustmentText);
            }

            _adjustmentText = null;
            _adjustmentStartedAt = 0;
            _hoverStartedAt = unchecked(now - HoverDelayMilliseconds);
            return new Presentation(PresentationKind.AppName);
        }

        return Elapsed(now, _hoverStartedAt) >= HoverDelayMilliseconds
            ? new Presentation(PresentationKind.AppName)
            : new Presentation(PresentationKind.Hidden);
    }

    /// <summary>
    /// Stops the session and requests a hidden presentation.
    /// </summary>
    public Presentation Stop()
    {
        _isHovering = false;
        _hoverStartedAt = 0;
        _adjustmentText = null;
        _adjustmentStartedAt = 0;
        return new Presentation(PresentationKind.Hidden);
    }

    private static long Elapsed(long now, long startedAt)
        => unchecked(now - startedAt);
}

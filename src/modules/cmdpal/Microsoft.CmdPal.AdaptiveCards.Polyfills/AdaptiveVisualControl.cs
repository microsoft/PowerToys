// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>
/// Base class for natively rendered elements whose look depends on the theme and width. New
/// element versions are applied in place, so live data doesn't replace the card.
/// </summary>
internal abstract partial class AdaptiveVisualControl : UserControl, IIncrementalAdaptiveElementControl
{
    private double _renderedWidth = double.NaN;

    protected AdaptiveVisualControl()
    {
        IsTabStop = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        ActualThemeChanged += (_, _) => Render();
        SizeChanged += (_, e) =>
        {
            if (WidthAffectsLayout && Math.Abs(e.NewSize.Width - _renderedWidth) > 0.5)
            {
                Render();
            }
        };
    }

    public abstract string IncrementalState { get; }

    protected bool IsDarkTheme => ActualTheme == ElementTheme.Dark;

    /// <summary>Gets a value indicating whether the visuals must be rebuilt when the width changes.</summary>
    protected virtual bool WidthAffectsLayout => true;

    /// <summary>Gets the width to lay out for; before the first layout pass a default is used.</summary>
    protected double LayoutWidth => ActualWidth > 0 ? ActualWidth : 320;

    public bool CanApplyIncrementalState(IIncrementalAdaptiveElementControl candidate) =>
        candidate.GetType() == GetType();

    public void ApplyIncrementalState(IIncrementalAdaptiveElementControl candidate)
    {
        ApplyModel((AdaptiveVisualControl)candidate);
        Render();
    }

    /// <summary>Takes the model of a newer control of the same type.</summary>
    protected abstract void ApplyModel(AdaptiveVisualControl candidate);

    /// <summary>Rebuilds the visuals for the current model, theme, and width.</summary>
    protected void Render()
    {
        _renderedWidth = ActualWidth;
        RenderCore();
    }

    protected abstract void RenderCore();
}

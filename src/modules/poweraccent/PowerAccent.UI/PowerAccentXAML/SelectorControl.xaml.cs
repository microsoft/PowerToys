// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace PowerAccent.UI;

/// <summary>
/// The accent selector content. Hosting it in a UserControl (rather than directly in the
/// TransparentWindow) lets x:Bind initialize on the control's Loading pass - which fires when the
/// SW_SHOWNA overlay is first laid out - instead of on Window.Activated (which never fires for a
/// never-activated overlay). That removes the need to call Bindings.Update() by hand.
/// </summary>
public sealed partial class SelectorControl : UserControl
{
    public SelectorViewModel ViewModel { get; } = new();

    public SelectorControl()
    {
        InitializeComponent();
    }

    // Number of items currently in the accent bar (mirrors the bound ObservableCollection).
    public int ItemCount => CharactersList.Items.Count;

    // The window sizing calculation must reserve all horizontal space outside the ListView: the
    // Surface's outer margin plus its border (1px each side from DefaultTransientSurfaceStyle).
    // Reading both from the live element means the formula stays correct if either value changes.
    internal double HorizontalSurfaceOverheadDip =>
        Surface.Margin.Left + Surface.Margin.Right +
        Surface.BorderThickness.Left + Surface.BorderThickness.Right;

    /// <summary>
    /// Measures the accent list against an unbounded width and returns the width its items actually
    /// need, in DIP - 0 when it cannot be measured yet.
    /// </summary>
    /// <remarks>
    /// The cell is a <c>MinWidth</c> of 48, not a fixed 48: a glyph wider than that (₹, ‰, ﷼, a CJK
    /// fallback) grows its cell, so the bar has to be measured rather than derived from the item
    /// count. The measurement is taken explicitly instead of read from the last layout pass because
    /// the bar is rebuilt on every summon while its shown window is still cloaked, so the caller
    /// first lays out the newly visible surface off screen. Measuring against an infinite width also
    /// yields the true content width rather than whatever the ScrollViewer inside the ListView's own
    /// template would have clipped it to.
    /// </remarks>
    internal double MeasureContentWidthDip()
    {
        CharactersList.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double width = CharactersList.DesiredSize.Width;
        return double.IsFinite(width) ? width : 0;
    }

    // Wire the inner TransientSurface to the hosting window's Show/Hide so it animates in/out.
    // TransientSurface.SubscribeTo explicitly supports being "placed within" the window content.
    public void SubscribeSurfaceTo(TransparentWindow host) => Surface.SubscribeTo(host);

    public void SetSelectedIndex(int index) => CharactersList.SelectedIndex = index;

    public void ScrollSelectedIntoView(int index)
    {
        if (index >= 0 && index < CharactersList.Items.Count)
        {
            CharactersList.ScrollIntoView(CharactersList.Items[index]);
        }
    }
}

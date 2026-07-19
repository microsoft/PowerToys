// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml.Controls;

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

    // The window sizing calculation must reserve the space outside the accent list as well as the
    // items themselves. Read it from the surface so the calculation stays in sync with the XAML.
    internal double HorizontalSurfaceMarginDip => Surface.Margin.Left + Surface.Margin.Right;

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

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Base class for a page that renders a strip of tabs, where each tab is its
/// own independent <see cref="IPage"/>.
/// </summary>
/// <remarks>
/// This type implements <b>both</b> <see cref="ITabbedPage"/> and
/// <see cref="IContentPage"/>. Newer Command Palette hosts prefer
/// <see cref="ITabbedPage"/> and render the tab strip. Older hosts that predate
/// tabbed pages don't recognize <see cref="ITabbedPage"/>; they fall back to
/// <see cref="IContentPage"/> and render <see cref="GetContent"/>, which by
/// default shows a message asking the user to update Command Palette. Override
/// <see cref="GetContent"/> or set <see cref="UpdateMessage"/> to customize that
/// fallback.
/// </remarks>
public abstract partial class TabbedPage : Page, ITabbedPage, IContentPage
{
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    /// <summary>
    /// Gets or sets the markdown message shown by the <see cref="IContentPage"/>
    /// fallback on hosts that don't support tabbed pages.
    /// </summary>
    public virtual string UpdateMessage { get; set => SetProperty(ref field, value); }
        = "This extension uses a tabbed page, which requires a newer version of Command Palette. Please update Command Palette to use this extension.";

    /// <summary>
    /// Returns the tabs for this page. Raise <see cref="ItemsChanged"/> (via
    /// <see cref="RaiseItemsChanged"/>) when the set of tabs changes; the host
    /// re-reads this method and preserves the active tab when it still exists.
    /// </summary>
    public abstract ITab[] GetTabs();

    //// IContentPage fallback members (used only by hosts without tabbed-page support) ////

    public virtual IDetails? Details { get; set => SetProperty(ref field, value); }

    public virtual IContextItem[] Commands { get; set => SetProperty(ref field, value); } = [];

    public virtual IContent[] GetContent() => [new MarkdownContent(UpdateMessage)];

    protected void RaiseItemsChanged(int totalItems = -1)
    {
        try
        {
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }
}

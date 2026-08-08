// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A single tab within a <see cref="TabbedPage"/>. Wraps an <see cref="IPage"/>
/// (an <c>IListPage</c>, <c>IDynamicListPage</c> or <c>IContentPage</c> in v1)
/// and carries its own tab chrome (title, icon, badge) so the tab strip can be
/// rendered before the hosted page is initialized.
/// </summary>
public partial class Tab : BaseObservable, ITab
{
    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual IIconInfo? Icon { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets a short badge shown after the tab label (for example a
    /// count like "10" or "99+"). This is observable: extensions can leave it
    /// empty initially and populate it asynchronously - setting it raises a
    /// <c>PropChanged("Badge")</c> so the host updates the tab strip without
    /// having to initialize the tab's page.
    /// </summary>
    public virtual string Badge { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual IPage Page { get; set => SetProperty(ref field, value); } = null!;

    public Tab()
    {
    }

    public Tab(IPage page)
    {
        Page = page;
    }

    public Tab(string title, IPage page)
    {
        Title = title;
        Page = page;
    }
}

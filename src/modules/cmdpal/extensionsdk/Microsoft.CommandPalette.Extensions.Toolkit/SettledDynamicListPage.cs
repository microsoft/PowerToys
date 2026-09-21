// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A dynamic list page that can tell Command Palette when the latest
/// <see cref="ListPage.GetItems"/> snapshot is the settled ranking for a query.
/// Implement this when a fast type-then-Enter should run that ranking.
/// Pages that do not implement <see cref="ISettledSearchSource"/> are never auto-activated.
/// </summary>
public abstract partial class SettledDynamicListPage : DynamicListPage, ISettledSearchSource
{
    public event TypedEventHandler<object, ISearchSettlementChangedEventArgs>? SearchSettlementChanged;

    public abstract bool CurrentFetchIsSettledFor(string query);

    /// <summary>
    /// Notifies the host that <see cref="CurrentFetchIsSettledFor"/> may now be true.
    /// Also raise <see cref="ListPage.ItemsChanged"/> so the host fetches that snapshot.
    /// </summary>
    protected void RaiseSearchSettlementChanged()
    {
        try
        {
            SearchSettlementChanged?.Invoke(this, new SearchSettlementChangedEventArgs());
        }
        catch
        {
        }
    }
}

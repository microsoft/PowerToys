// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Lets a list page tell the host which query the latest <c>GetItems()</c> snapshot belongs to,
/// and when that ranking is safe to activate. Dynamic pages that do not implement this are never
/// auto-activated: <c>ItemsChanged</c> alone does not identify a query (GH #48670).
/// </summary>
internal interface ISettledSearchSource
{
    /// <summary>
    /// True when the most recent <c>GetItems()</c> call built the settled ranking for <paramref name="query"/>.
    /// A fetch that ran before settlement stays false even if settlement lands before the host reads this.
    /// </summary>
    bool CurrentFetchIsSettledFor(string query);

    event EventHandler? SearchSettlementChanged;
}

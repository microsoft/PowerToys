// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal readonly record struct SearchCatalogStatus(uint PendingItemsCount, int? HResult)
{
    public bool IsAvailable => HResult is null;
}

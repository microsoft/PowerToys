// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Indexer;

internal interface IIndexerFallbackSearchEngine : IDisposable
{
    SearchNoticeInfo? Query(string query, uint queryCookie);

    IList<IListItem> FetchNextItems(int limit, uint queryCookie, out bool hasMore, out SearchNoticeInfo? notice);
}

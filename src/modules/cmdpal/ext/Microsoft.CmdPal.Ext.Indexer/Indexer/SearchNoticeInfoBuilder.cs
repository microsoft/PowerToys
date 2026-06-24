// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CmdPal.Ext.Indexer.Properties;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal static class SearchNoticeInfoBuilder
{
    private const int RpcServerUnavailable = unchecked((int)0x800706BA);
    private const int RpcDisconnected = unchecked((int)0x80010108);
    private const int RpcCallRejected = unchecked((int)0x80010001);
    private const int RpcServerCallRetryLater = unchecked((int)0x8001010A);
    private const int ServiceDisabled = unchecked((int)0x80070422);
    private const int ServiceNotActive = unchecked((int)0x80070426);
    private const int ClassNotRegistered = unchecked((int)0x80040154);
    private const int ServerExecutionFailed = unchecked((int)0x80080005);

    internal static SearchNoticeInfo? FromQueryStatus(SearchQuery.SearchExecutionStatus status)
    {
        return status.State switch
        {
            SearchQuery.QueryState.NullDataSource or
            SearchQuery.QueryState.CreateSessionFailed or
            SearchQuery.QueryState.CreateCommandFailed => CreateUnavailableNotice(),

            SearchQuery.QueryState.ExecuteFailed when IsSearchUnavailableHResult(status.HResult) => CreateUnavailableNotice(),
            SearchQuery.QueryState.ExecuteFailed => CreateSearchFailedNotice(),
            _ => null,
        };
    }

    [SuppressMessage("Performance", "CA1863:Cache a 'CompositeFormat' for repeated use in this formatting operation", Justification = "Formatting a low-frequency user-visible notice once per query is sufficient.")]
    internal static SearchNoticeInfo? FromCatalogStatus(SearchCatalogStatus status)
    {
        if (status.PendingItemsCount > 0)
        {
            return new SearchNoticeInfo(
                Resources.Indexer_SearchIndexingMessage,
                string.Format(CultureInfo.CurrentCulture, Resources.Indexer_SearchIndexingMessageTip, status.PendingItemsCount));
        }

        return null;
    }

    private static SearchNoticeInfo CreateUnavailableNotice() =>
        new(Resources.Indexer_SearchUnavailableMessage, Resources.Indexer_SearchUnavailableMessageTip);

    private static SearchNoticeInfo CreateSearchFailedNotice() =>
        new(Resources.Indexer_SearchFailedMessage, Resources.Indexer_SearchFailedMessageTip);

    private static bool IsSearchUnavailableHResult(int? hresult) =>
        hresult is RpcServerUnavailable
            or RpcDisconnected
            or RpcCallRejected
            or RpcServerCallRetryLater
            or ServiceDisabled
            or ServiceNotActive
            or ClassNotRegistered
            or ServerExecutionFailed;
}

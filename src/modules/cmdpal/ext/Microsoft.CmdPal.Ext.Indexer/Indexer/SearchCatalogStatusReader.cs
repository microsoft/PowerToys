// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using ISearchCatalogManager = Windows.Win32.System.Search.ISearchCatalogManager.Interface;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal static class SearchCatalogStatusReader
{
    private const string SystemIndex = "SystemIndex";
    private static readonly Lock FailureLoggingLock = new();
    private static int? _lastLoggedFailureHResult;

    internal static SearchCatalogStatus GetStatus()
    {
        try
        {
            var catalogManager = CreateCatalogManager();
            var pendingItemsCount = catalogManager.NumberOfItemsToIndex();
            ResetFailureLoggingState();
            return new SearchCatalogStatus(pendingItemsCount, null);
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            return new SearchCatalogStatus(0, ex.HResult);
        }
    }

    private static ISearchCatalogManager CreateCatalogManager()
    {
        var searchManager = ComHelper.CreateComInstance<ISearchManager>(ref Unsafe.AsRef(in CLSID.SearchManager), CLSCTX.LocalServer);
        var catalogManager = searchManager.GetCatalog(SystemIndex);
        return catalogManager ?? throw new ArgumentException($"Failed to get catalog manager for {SystemIndex}");
    }

    private static void LogFailure(Exception ex)
    {
        var shouldLogWarning = false;

        lock (FailureLoggingLock)
        {
            if (_lastLoggedFailureHResult != ex.HResult)
            {
                _lastLoggedFailureHResult = ex.HResult;
                shouldLogWarning = true;
            }
        }

        var message = $"Failed to read Windows Search catalog status. HResult=0x{ex.HResult:X8}, Message={ex.Message}";
        if (shouldLogWarning)
        {
            Logger.LogWarning(message);
        }
        else
        {
            Logger.LogDebug(message);
        }
    }

    private static void ResetFailureLoggingState()
    {
        lock (FailureLoggingLock)
        {
            _lastLoggedFailureHResult = null;
        }
    }
}

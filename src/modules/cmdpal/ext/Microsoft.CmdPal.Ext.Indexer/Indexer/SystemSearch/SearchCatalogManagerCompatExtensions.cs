// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.System.Search;
using ISearchCatalogManager = Windows.Win32.System.Search.ISearchCatalogManager.Interface;
namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

internal static class SearchCatalogManagerCompatExtensions
{
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public static uint NumberOfItemsToIndex(this ISearchCatalogManager catalogManager)
    {
        var hr = catalogManager.NumberOfItemsToIndex(out var incrementalCount, out _, out _);
        Marshal.ThrowExceptionForHR(hr.Value);
        return checked((uint)incrementalCount);
    }

    public static unsafe ISearchQueryHelper GetQueryHelper(this ISearchCatalogManager catalogManager)
    {
        ISearchQueryHelper* queryHelperPtr;
        var hr = catalogManager.GetQueryHelper(&queryHelperPtr);
        Marshal.ThrowExceptionForHR(hr.Value);

        if (queryHelperPtr is null)
        {
            throw new ArgumentException("Failed to retrieve ISearchQueryHelper. Returned null pointer.");
        }

        var queryHelperComPtr = (IntPtr)queryHelperPtr;
        try
        {
            var comObject = ComWrappers.GetOrCreateObjectForComInstance(queryHelperComPtr, CreateObjectFlags.None);
            if (comObject is not ISearchQueryHelper queryHelper)
            {
                throw new ArgumentException("Failed to create ISearchQueryHelper managed instance.");
            }

            return queryHelper;
        }
        finally
        {
            Marshal.Release(queryHelperComPtr);
        }
    }
}

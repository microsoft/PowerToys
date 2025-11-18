// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal sealed partial class SearchQuery : IDisposable
{
    private readonly Lock _lockObject = new(); // Lock object for synchronization
    private readonly DBPROPIDSET dbPropIdSet;

    private uint reuseWhereID;
    private EventWaitHandle queryCompletedEvent;
    private Timer queryTpTimer;
    private IRowset currentRowset;
    private IRowset reuseRowset;

    public uint Cookie { get; set; }

    public string SearchText { get; private set; }

    public ConcurrentQueue<SearchResult> SearchResults { get; private set; } = [];

    public SearchQuery()
    {
        dbPropIdSet = new DBPROPIDSET
        {
            rgPropertyIDs = Marshal.AllocCoTaskMem(sizeof(uint)), // Allocate memory for the property ID array
            cPropertyIDs = 1,
            guidPropertySet = new Guid("AA6EE6B0-E828-11D0-B23E-00AA0047FC01"), // DBPROPSET_MSIDXS_ROWSETEXT,
        };

        // Copy the property ID into the allocated memory
        Marshal.WriteInt32(dbPropIdSet.rgPropertyIDs, 8); // MSIDXSPROP_WHEREID

        Init();
    }

    private void Init()
    {
        // Create all the objects we will want cached
        try
        {
            queryTpTimer = new Timer(QueryTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            if (queryTpTimer is null)
            {
                Logger.LogError("Failed to create query timer");
                return;
            }

            queryCompletedEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
            if (queryCompletedEvent is null)
            {
                Logger.LogError("Failed to create query completed event");
                return;
            }

            // Execute a synchronous query on file items to prime the index and keep that handle around
            PrimeIndexAndCacheWhereId();
        }
        catch (Exception ex)
        {
            Logger.LogError("Exception at SearchUXQueryHelper Init", ex);
        }
    }

    public void WaitForQueryCompletedEvent() => queryCompletedEvent.WaitOne();

    public void CancelOutstandingQueries()
    {
        Logger.LogDebug("Cancel query " + SearchText);

        // Are we currently doing work? If so, let's cancel
        lock (_lockObject)
        {
            if (queryTpTimer is not null)
            {
                queryTpTimer.Change(Timeout.Infinite, Timeout.Infinite);
                queryTpTimer.Dispose();
                queryTpTimer = null;
            }

            Init();
        }
    }

    public void Execute(string searchText, uint cookie)
    {
        SearchText = searchText;
        Cookie = cookie;
        ExecuteSyncInternal();
    }

    public static void QueryTimerCallback(object state)
    {
        var pQueryHelper = (SearchQuery)state;
        pQueryHelper.ExecuteSyncInternal();
    }

    private void ExecuteSyncInternal()
    {
        lock (_lockObject)
        {
            var queryStr = QueryStringBuilder.GenerateQuery(SearchText, reuseWhereID);
            try
            {
                // We need to generate a search query string with the search text the user entered above
                if (currentRowset is not null)
                {
                    // We have a previous rowset, this means the user is typing and we should store this
                    // recapture the where ID from this so the next ExecuteSync call will be faster
                    reuseRowset = currentRowset;
                    reuseWhereID = GetReuseWhereId(reuseRowset);
                }

                currentRowset = ExecuteCommand(queryStr);

                SearchResults.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing query", ex);
            }
            finally
            {
                queryCompletedEvent.Set();
            }
        }
    }

    private bool HandleRow(IGetRow getRow, nuint rowHandle)
    {
        try
        {
            getRow.GetRowFromHROW(null, rowHandle, ref Unsafe.AsRef(in IID.IPropertyStore), out var propertyStore);

            if (propertyStore is null)
            {
                Logger.LogError("Failed to get IPropertyStore interface");
                return false;
            }

            var searchResult = SearchResult.Create(propertyStore);
            if (searchResult is null)
            {
                Logger.LogError("Failed to create search result");
                return false;
            }

            SearchResults.Enqueue(searchResult);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error handling row", ex);
            return false;
        }
    }

    public bool FetchRows(int offset, int limit)
    {
        if (currentRowset is null)
        {
            Logger.LogError("No rowset to fetch rows from");
            return false;
        }

        IGetRow getRow = null;

        try
        {
            getRow = (IGetRow)currentRowset;
        }
        catch (Exception)
        {
            Logger.LogInfo("Reset the current rowset");
            ExecuteSyncInternal();
            getRow = (IGetRow)currentRowset;
        }

        uint rowCountReturned;
        var prghRows = IntPtr.Zero;

        try
        {
            currentRowset.GetNextRows(IntPtr.Zero, offset, limit, out rowCountReturned, out prghRows);

            if (rowCountReturned == 0)
            {
                // No more rows to fetch
                return false;
            }

            // Marshal the row handles
            var rowHandles = new IntPtr[rowCountReturned];
            Marshal.Copy(prghRows, rowHandles, 0, (int)rowCountReturned);

            for (var i = 0; i < rowCountReturned; i++)
            {
                var rowHandle = Marshal.ReadIntPtr(prghRows, i * IntPtr.Size);
                if (!HandleRow(getRow, (nuint)rowHandle))
                {
                    break;
                }
            }

            currentRowset.ReleaseRows(rowCountReturned, rowHandles, IntPtr.Zero, null, null);

            Marshal.FreeCoTaskMem(prghRows);
            prghRows = IntPtr.Zero;

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error fetching rows", ex);
            return false;
        }
        finally
        {
            if (prghRows != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(prghRows);
            }
        }
    }

    private void PrimeIndexAndCacheWhereId()
    {
        var queryStr = QueryStringBuilder.GeneratePrimingQuery();
        var rowset = ExecuteCommand(queryStr);
        if (rowset is not null)
        {
            reuseRowset = rowset;
            reuseWhereID = GetReuseWhereId(reuseRowset);
        }
    }

    private unsafe IRowset ExecuteCommand(string queryStr)
    {
        if (string.IsNullOrEmpty(queryStr))
        {
            return null;
        }

        try
        {
            var session = (IDBCreateSession)DataSourceManager.GetDataSource();
            var guid = typeof(IDBCreateCommand).GUID;
            session.CreateSession(IntPtr.Zero, ref guid, out var ppDBSession);

            if (ppDBSession is null)
            {
                Logger.LogError("CreateSession failed");
                return null;
            }

            var createCommand = (IDBCreateCommand)ppDBSession;
            guid = typeof(ICommandText).GUID;
            createCommand.CreateCommand(IntPtr.Zero, ref guid, out ICommandText commandText);

            if (commandText is null)
            {
                Logger.LogError("Failed to get ICommandText interface");
                return null;
            }

            var riid = NativeHelpers.OleDb.DbGuidDefault;

            var irowSetRiid = typeof(IRowset).GUID;

            commandText.SetCommandText(ref riid, queryStr);
            commandText.Execute(null, ref irowSetRiid, null, out var pcRowsAffected, out var rowsetPointer);

            return rowsetPointer;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error.", ex);
            return null;
        }
    }

    private unsafe DBPROP? GetPropset(IRowsetInfo rowsetInfo)
    {
        var prgPropSetsPtr = IntPtr.Zero;

        try
        {
            ulong cPropertySets;
            var res = rowsetInfo.GetProperties(1, [dbPropIdSet], out cPropertySets, out prgPropSetsPtr);
            if (res != 0)
            {
                Logger.LogError($"Error getting properties: {res}");
                return null;
            }

            if (cPropertySets == 0 || prgPropSetsPtr == IntPtr.Zero)
            {
                Logger.LogError("No property sets returned");
                return null;
            }

            var firstPropSetPtr = (DBPROPSET*)prgPropSetsPtr.ToInt64();
            var propSet = *firstPropSetPtr;
            if (propSet.cProperties == 0 || propSet.rgProperties == IntPtr.Zero)
            {
                return null;
            }

            var propPtr = (DBPROP*)propSet.rgProperties.ToInt64();
            return *propPtr;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception occurred while getting properties,", ex);
            return null;
        }
        finally
        {
            // Free the property sets pointer returned by GetProperties, if necessary
            if (prgPropSetsPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(prgPropSetsPtr);
            }
        }
    }

    private uint GetReuseWhereId(IRowset rowset)
    {
        var rowsetInfo = (IRowsetInfo)rowset;

        if (rowsetInfo is null)
        {
            return 0;
        }

        var prop = GetPropset(rowsetInfo);
        if (prop is null)
        {
            return 0;
        }

        if (prop?.vValue.VarType == VarEnum.VT_UI4)
        {
            var value = prop?.vValue._ulong;
            return (uint)value;
        }

        return 0;
    }

    public void Dispose()
    {
        CancelOutstandingQueries();

        // Free the allocated memory for rgPropertyIDs
        if (dbPropIdSet.rgPropertyIDs != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(dbPropIdSet.rgPropertyIDs);
        }

        queryCompletedEvent?.Dispose();
    }
}

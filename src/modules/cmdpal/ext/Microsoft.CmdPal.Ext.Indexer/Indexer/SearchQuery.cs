// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.System.Search;
using Windows.Win32.UI.Shell.PropertiesSystem;

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
            if (queryTpTimer == null)
            {
                Logger.LogError("Failed to create query timer");
                return;
            }

            queryCompletedEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
            if (queryCompletedEvent == null)
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
            if (queryTpTimer != null)
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
                if (currentRowset != null)
                {
                    if (reuseRowset != null)
                    {
                        Marshal.ReleaseComObject(reuseRowset);
                    }

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
        object propertyStorePtr = null;

        try
        {
            getRow.GetRowFromHROW(null, rowHandle, typeof(IPropertyStore).GUID, out propertyStorePtr);

            var propertyStore = (IPropertyStore)propertyStorePtr;
            if (propertyStore == null)
            {
                Logger.LogError("Failed to get IPropertyStore interface");
                return false;
            }

            var searchResult = SearchResult.Create(propertyStore);
            if (searchResult == null)
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
        finally
        {
            // Ensure the COM object is released if not returned
            if (propertyStorePtr != null)
            {
                Marshal.ReleaseComObject(propertyStorePtr);
            }
        }
    }

    public bool FetchRows(int offset, int limit)
    {
        if (currentRowset == null)
        {
            Logger.LogError("No rowset to fetch rows from");
            return false;
        }

        if (currentRowset is not IGetRow)
        {
            Logger.LogInfo("Reset the current rowset");
            ExecuteSyncInternal();
        }

        if (currentRowset is not IGetRow getRow)
        {
            Logger.LogError("Rowset does not support IGetRow interface");
            return false;
        }

        uint rowCountReturned;
        var prghRows = IntPtr.Zero;

        try
        {
            var res = currentRowset.GetNextRows(IntPtr.Zero, offset, limit, out rowCountReturned, out prghRows);
            if (res < 0)
            {
                Logger.LogError($"Error fetching rows: {res}");
                return false;
            }

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

            res = currentRowset.ReleaseRows(rowCountReturned, rowHandles, IntPtr.Zero, null, null);
            if (res != 0)
            {
                Logger.LogError($"Error releasing rows: {res}");
            }

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
        if (rowset != null)
        {
            if (reuseRowset != null)
            {
                Marshal.ReleaseComObject(reuseRowset);
            }

            reuseRowset = rowset;
            reuseWhereID = GetReuseWhereId(reuseRowset);
        }
    }

    private unsafe IRowset ExecuteCommand(string queryStr)
    {
        object sessionPtr = null;
        object commandPtr = null;

        try
        {
            var session = (IDBCreateSession)DataSourceManager.GetDataSource();
            session.CreateSession(null, typeof(IDBCreateCommand).GUID, out sessionPtr);
            if (sessionPtr == null)
            {
                Logger.LogError("CreateSession failed");
                return null;
            }

            var createCommand = (IDBCreateCommand)sessionPtr;
            createCommand.CreateCommand(null, typeof(ICommandText).GUID, out commandPtr);
            if (commandPtr == null)
            {
                Logger.LogError("CreateCommand failed");
                return null;
            }

            var commandText = (ICommandText)commandPtr;
            if (commandText == null)
            {
                Logger.LogError("Failed to get ICommandText interface");
                return null;
            }

            commandText.SetCommandText(in NativeHelpers.OleDb.DbGuidDefault, queryStr);
            commandText.Execute(null, typeof(IRowset).GUID, null, null, out var rowsetPointer);

            return rowsetPointer as IRowset;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error.", ex);
            return null;
        }
        finally
        {
            // Release the command pointer
            if (commandPtr != null)
            {
                Marshal.ReleaseComObject(commandPtr);
            }

            // Release the session pointer
            if (sessionPtr != null)
            {
                Marshal.ReleaseComObject(sessionPtr);
            }
        }
    }

    private IRowsetInfo GetRowsetInfo(IRowset rowset)
    {
        if (rowset == null)
        {
            return null;
        }

        var rowsetPtr = IntPtr.Zero;
        var rowsetInfoPtr = IntPtr.Zero;

        try
        {
            // Get the IUnknown pointer for the IRowset object
            rowsetPtr = Marshal.GetIUnknownForObject(rowset);

            // Query for IRowsetInfo interface
            var rowsetInfoGuid = typeof(IRowsetInfo).GUID;
            var res = Marshal.QueryInterface(rowsetPtr, in rowsetInfoGuid, out rowsetInfoPtr);
            if (res != 0)
            {
                Logger.LogError($"Error getting IRowsetInfo interface: {res}");
                return null;
            }

            // Marshal the interface pointer to the actual IRowsetInfo object
            var rowsetInfo = (IRowsetInfo)Marshal.GetObjectForIUnknown(rowsetInfoPtr);
            return rowsetInfo;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception occurred while getting IRowsetInfo. ", ex);
            return null;
        }
        finally
        {
            // Release the IRowsetInfo pointer if it was obtained
            if (rowsetInfoPtr != IntPtr.Zero)
            {
                Marshal.Release(rowsetInfoPtr); // Release the IRowsetInfo pointer
            }

            // Release the IUnknown pointer for the IRowset object
            if (rowsetPtr != IntPtr.Zero)
            {
                Marshal.Release(rowsetPtr);
            }
        }
    }

    private DBPROP? GetPropset(IRowsetInfo rowsetInfo)
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

            var firstPropSetPtr = new IntPtr(prgPropSetsPtr.ToInt64());
            var propSet = Marshal.PtrToStructure<DBPROPSET>(firstPropSetPtr);
            if (propSet.cProperties == 0 || propSet.rgProperties == IntPtr.Zero)
            {
                return null;
            }

            var propPtr = new IntPtr(propSet.rgProperties.ToInt64());
            var prop = Marshal.PtrToStructure<DBPROP>(propPtr);
            return prop;
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
        var rowsetInfo = GetRowsetInfo(rowset);
        if (rowsetInfo == null)
        {
            return 0;
        }

        var prop = GetPropset(rowsetInfo);
        if (prop == null)
        {
            return 0;
        }

        if (prop?.vValue.Anonymous.Anonymous.vt == VARENUM.VT_UI4)
        {
            var value = prop?.vValue.Anonymous.Anonymous.Anonymous.ulVal;
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

        if (reuseRowset != null)
        {
            Marshal.ReleaseComObject(reuseRowset);
            reuseRowset = null;
        }

        if (currentRowset != null)
        {
            Marshal.ReleaseComObject(currentRowset);
            currentRowset = null;
        }

        queryCompletedEvent?.Dispose();
    }
}

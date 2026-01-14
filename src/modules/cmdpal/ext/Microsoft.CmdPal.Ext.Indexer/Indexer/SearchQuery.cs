// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Indexing Service constant")]
    private const int QUERY_E_ALLNOISE = unchecked((int)0x80041605);

    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Indexing Service constant")]
    private const uint MSIDXSPROP_WHEREID = 8;

    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Indexing Service constant")]
    private const uint MSIDXSPROP_RESULTS_FOUND = 7;

    private readonly Lock _lockObject = new();
    private readonly DBPROPIDSET dbPropIdSet;

    private IRowset currentRowset;

    public QueryState State { get; private set; } = QueryState.NotStarted;

    public int? TotalResultsFound { get; private set; }

    private int? LastHResult { get; set; }

    private string LastErrorMessage { get; set; }

    public uint Cookie { get; private set; }

    public string SearchText { get; private set; }

    public ConcurrentQueue<SearchResult> SearchResults { get; private set; } = [];

    public SearchQuery()
    {
        dbPropIdSet = new DBPROPIDSET
        {
            rgPropertyIDs = Marshal.AllocCoTaskMem(sizeof(uint) * 2),
            cPropertyIDs = 2,
            guidPropertySet = new Guid("AA6EE6B0-E828-11D0-B23E-00AA0047FC01"), // DBPROPSET_MSIDXS_ROWSETEXT
        };

        // Property IDs are an array of uint.
        Marshal.WriteInt32(dbPropIdSet.rgPropertyIDs, checked((int)MSIDXSPROP_WHEREID));
        Marshal.WriteInt32(dbPropIdSet.rgPropertyIDs, sizeof(uint), checked((int)MSIDXSPROP_RESULTS_FOUND));
    }

    public void CancelOutstandingQueries()
    {
        Logger.LogDebug("Cancel query " + SearchText);

        // Are we currently doing work? If so, let's cancel
        lock (_lockObject)
        {
            State = QueryState.Cancelled;
        }
    }

    public int Execute(string searchText, uint cookie)
    {
        SearchText = searchText;
        Cookie = cookie;
        ExecuteSyncInternal();
        return TotalResultsFound ?? 0;
    }

    private void ExecuteSyncInternal()
    {
        lock (_lockObject)
        {
            State = QueryState.Running;
            LastHResult = null;
            LastErrorMessage = null;
            TotalResultsFound = null;

            var queryStr = QueryStringBuilder.GenerateQuery(SearchText);
            try
            {
                var result = ExecuteCommand(queryStr);
                currentRowset = result.Rowset;
                State = result.State;
                LastHResult = result.HResult;
                LastErrorMessage = result.ErrorMessage;

                TotalResultsFound = TryGetTotalResultsFound(currentRowset);

                SearchResults.Clear();
            }
            catch (Exception ex)
            {
                State = QueryState.ExecuteFailed;
                LastHResult = ex.HResult;
                LastErrorMessage = ex.Message;
                Logger.LogError("Error executing query", ex);
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
            var message = $"No rowset to fetch rows from. State={State}, TotalResultsFound={TotalResultsFound}, HResult={LastHResult}, Error='{LastErrorMessage}'";

            switch (State)
            {
                case QueryState.NoResults:
                case QueryState.AllNoise:
                    Logger.LogDebug(message);
                    break;
                case QueryState.NotStarted:
                case QueryState.Cancelled:
                case QueryState.Running:
                    Logger.LogInfo(message);
                    break;
                default:
                    Logger.LogError(message);
                    break;
            }

            return false;
        }

        IGetRow getRow;

        try
        {
            getRow = (IGetRow)currentRowset;
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"Reset the current rowset. State={State}, HResult={LastHResult}, Error='{LastErrorMessage}'");
            Logger.LogError("Failed to cast current rowset to IGetRow", ex);

            ExecuteSyncInternal();

            if (currentRowset is null)
            {
                var message = $"Failed to reset rowset. State={State}, HResult={LastHResult}, Error='{LastErrorMessage}'";

                switch (State)
                {
                    case QueryState.NoResults:
                    case QueryState.AllNoise:
                        Logger.LogDebug(message);
                        break;
                    default:
                        Logger.LogError(message);
                        break;
                }

                return false;
            }

            getRow = (IGetRow)currentRowset;
        }

        var prghRows = IntPtr.Zero;

        try
        {
            currentRowset.GetNextRows(IntPtr.Zero, offset, limit, out var rowCountReturned, out prghRows);

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

    private static ExecuteCommandResult ExecuteCommand(string queryStr)
    {
        if (string.IsNullOrEmpty(queryStr))
        {
            return new ExecuteCommandResult(Rowset: null, State: QueryState.ExecuteFailed, HResult: null, ErrorMessage: "Query string was empty.");
        }

        try
        {
            var dataSource = DataSourceManager.GetDataSource();
            if (dataSource is null)
            {
                Logger.LogError("GetDataSource returned null");
                return new ExecuteCommandResult(Rowset: null, State: QueryState.NullDataSource, HResult: null, ErrorMessage: "GetDataSource returned null.");
            }

            var session = (IDBCreateSession)dataSource;
            var guid = typeof(IDBCreateCommand).GUID;
            session.CreateSession(IntPtr.Zero, ref guid, out var ppDBSession);

            if (ppDBSession is null)
            {
                Logger.LogError("CreateSession failed");
                return new ExecuteCommandResult(Rowset: null, State: QueryState.CreateSessionFailed, HResult: null, ErrorMessage: "CreateSession returned null session.");
            }

            var createCommand = (IDBCreateCommand)ppDBSession;
            guid = typeof(ICommandText).GUID;
            createCommand.CreateCommand(IntPtr.Zero, ref guid, out var commandText);

            if (commandText is null)
            {
                Logger.LogError("Failed to get ICommandText interface");
                return new ExecuteCommandResult(Rowset: null, State: QueryState.CreateCommandFailed, HResult: null, ErrorMessage: "CreateCommand returned null command.");
            }

            var riid = NativeHelpers.OleDb.DbGuidDefault;
            var irowSetRiid = typeof(IRowset).GUID;

            commandText.SetCommandText(ref riid, queryStr);
            commandText.Execute(null, ref irowSetRiid, null, out _, out var rowsetPointer);

            return rowsetPointer is null
                ? new ExecuteCommandResult(Rowset: null, State: QueryState.NoResults, HResult: null, ErrorMessage: null)
                : new ExecuteCommandResult(Rowset: rowsetPointer, State: QueryState.Completed, HResult: null, ErrorMessage: null);
        }
        catch (COMException ex) when (ex.HResult == QUERY_E_ALLNOISE)
        {
            Logger.LogDebug($"Query returned all noise, no results. ({queryStr})");
            return new ExecuteCommandResult(Rowset: null, State: QueryState.AllNoise, HResult: ex.HResult, ErrorMessage: ex.Message);
        }
        catch (COMException ex)
        {
            Logger.LogError($"Unexpected COM error for query '{queryStr}'.", ex);
            return new ExecuteCommandResult(Rowset: null, State: QueryState.ExecuteFailed, HResult: ex.HResult, ErrorMessage: ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unexpected error for query '{queryStr}'.", ex);
            return new ExecuteCommandResult(Rowset: null, State: QueryState.ExecuteFailed, HResult: ex.HResult, ErrorMessage: ex.Message);
        }
    }

    private int? TryGetTotalResultsFound(IRowset rowset)
    {
        if (rowset is not IRowsetInfo rowsetInfo)
        {
            return null;
        }

        var prop = GetPropset(rowsetInfo, MSIDXSPROP_RESULTS_FOUND);
        if (prop is null)
        {
            return null;
        }

        return prop.Value.vValue.VarType switch
        {
            VarEnum.VT_UI4 => (int)(prop.Value.vValue._ulong > int.MaxValue ? int.MaxValue : prop.Value.vValue._ulong),
            VarEnum.VT_I4 => unchecked((int)prop.Value.vValue._ulong),
            _ => null,
        };
    }

    private unsafe DBPROP? GetPropset(IRowsetInfo rowsetInfo, uint propertyId)
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

            var props = (DBPROP*)propSet.rgProperties.ToInt64();
            for (var i = 0; i < (int)propSet.cProperties; i++)
            {
                var prop = props[i];
                if (prop.dwPropertyID == propertyId)
                {
                    return prop;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError("Exception occurred while getting properties.", ex);
            return null;
        }
        finally
        {
            if (prgPropSetsPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(prgPropSetsPtr);
            }
        }
    }

    public void Dispose()
    {
        CancelOutstandingQueries();

        // Free the allocated memory for rgPropertyIDs
        if (dbPropIdSet.rgPropertyIDs != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(dbPropIdSet.rgPropertyIDs);
        }
    }

    internal enum QueryState
    {
        NotStarted = 0,
        Running,
        Completed,
        NoResults,
        AllNoise,
        NullDataSource,
        CreateSessionFailed,
        CreateCommandFailed,
        ExecuteFailed,
        Cancelled,
    }

    private readonly record struct ExecuteCommandResult(
        IRowset Rowset,
        QueryState State,
        int? HResult,
        string ErrorMessage);
}

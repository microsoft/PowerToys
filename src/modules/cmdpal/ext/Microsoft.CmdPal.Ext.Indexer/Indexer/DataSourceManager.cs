// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal static class DataSourceManager
{
    private static IDBInitialize _dataSource;

    public static IDBInitialize GetDataSource()
    {
        if (_dataSource == null)
        {
            InitializeDataSource();
        }

        return _dataSource;
    }

    private static bool InitializeDataSource()
    {
        var riid = typeof(IDBInitialize).GUID;
        var hr = Ole32.CoCreateInstance(ref Unsafe.AsRef(in CLSGUID.CollatorDataSource), IntPtr.Zero, (uint)CLSCTX.ALL, ref riid, out var dataSourceObjPtr);
        if (hr != 0)
        {
            Logger.LogError("CoCreateInstance failed: " + hr);
            return false;
        }

        if (dataSourceObjPtr == IntPtr.Zero)
        {
            Logger.LogError("CoCreateInstance failed: dataSourceObjPtr is null");
            return false;
        }

        var comWrappers = new StrategyBasedComWrappers();
        _dataSource = (IDBInitialize)comWrappers.GetOrCreateObjectForComInstance(dataSourceObjPtr, CreateObjectFlags.None);

        if (_dataSource == null)
        {
            Logger.LogError("CoCreateInstance failed: dataSourceObj is null");
            return false;
        }

        _dataSource.Initialize();

        if (dataSourceObjPtr != IntPtr.Zero)
        {
            Marshal.Release(dataSourceObjPtr);
        }

        return true;
    }
}

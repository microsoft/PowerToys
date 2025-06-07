// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using Microsoft.CmdPal.Ext.Indexer.Native;

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
        var hr = NativeMethods.CoCreateInstance(ref Unsafe.AsRef(in NativeHelpers.CsWin32GUID.CLSIDCollatorDataSource), IntPtr.Zero, NativeHelpers.CLSCTXINPROCALL, ref riid, out var dataSourceObjPtr);
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

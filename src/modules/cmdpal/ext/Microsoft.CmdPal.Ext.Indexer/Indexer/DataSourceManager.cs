// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Windows.Win32.System.Search;
using WinRT;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal static class DataSourceManager
{
    private static readonly Guid CLSIDCollatorDataSource = new("9E175B8B-F52A-11D8-B9A5-505054503030");

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
        uint clsctxInProcServer = 0x00000001;
        var hr = NativeMethods.CoCreateInstance(CLSIDCollatorDataSource, IntPtr.Zero, clsctxInProcServer, typeof(IDBInitialize).GUID, out var dataSourceObjPtr);
        if (hr != 0)
        {
            Logger.LogError("CoCreateInstance failed: " + hr);
            return false;
        }

        /*
        var comWrappers = new StrategyBasedComWrappers();
        var ptr = dataSourceObjPtr;
        var dataSourceObj = comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);

        // create datasource object from ptr
        // var dataSourceObj = Marshal.GetObjectForIUnknown(dataSourceObjPtr);
        if (dataSourceObj == null)
        {
            Logger.LogError("CoCreateInstance failed: dataSourceObj is null");
            return false;
        }*/

        if (dataSourceObjPtr == IntPtr.Zero)
        {
            Logger.LogError("CoCreateInstance failed: dataSourceObjPtr is null");
            return false;
        }

        _dataSource = MarshalInterface<IDBInitialize>.FromAbi(dataSourceObjPtr);
        _dataSource.Initialize();

        return true;
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.System.Search;

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
        var hr = PInvoke.CoCreateInstance(CLSIDCollatorDataSource, null, CLSCTX.CLSCTX_INPROC_SERVER, typeof(IDBInitialize).GUID, out var dataSourceObj);
        if (hr != 0)
        {
            Logger.LogError("CoCreateInstance failed: " + hr);
            return false;
        }

        if (dataSourceObj == null)
        {
            Logger.LogError("CoCreateInstance failed: dataSourceObj is null");
            return false;
        }

        _dataSource = (IDBInitialize)dataSourceObj;
        _dataSource.Initialize();

        return true;
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal static class DataSourceManager
{
    private static IDBInitialize _dataSource;

    public static IDBInitialize GetDataSource()
    {
        if (_dataSource is null)
        {
            InitializeDataSource();
        }

        return _dataSource;
    }

    private static bool InitializeDataSource()
    {
        var riid = typeof(IDBInitialize).GUID;

        try
        {
            _dataSource = ComHelper.CreateComInstance<IDBInitialize>(ref Unsafe.AsRef(in CLSID.CollatorDataSource), CLSCTX.InProcServer);
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to create datasource. ex: {e.Message}");
            return false;
        }

        _dataSource.Initialize();

        return true;
    }
}

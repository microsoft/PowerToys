// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

internal sealed class MockBookmarkDataSource : IBookmarkDataSource
{
    private string _jsonData;
    private int _saveCount;

    public Action BeforeSave { get; set; }

    public MockBookmarkDataSource(string initialJsonData = "[]")
    {
        _jsonData = initialJsonData;
    }

    public string GetBookmarkData()
    {
        return _jsonData;
    }

    public void SaveBookmarkData(string jsonData)
    {
        BeforeSave?.Invoke();
        _jsonData = jsonData;
        Interlocked.Increment(ref _saveCount);
    }

    public int SaveCount => Volatile.Read(ref _saveCount);
}

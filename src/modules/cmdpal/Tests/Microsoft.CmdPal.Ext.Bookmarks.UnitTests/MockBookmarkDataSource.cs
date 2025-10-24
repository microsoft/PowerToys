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
        _jsonData = jsonData;
        Interlocked.Increment(ref _saveCount);
    }

    public int SaveCount => Volatile.Read(ref _saveCount);

    // Waits until at least expectedMinSaves have occurred or the timeout elapses.
    // Returns true if the condition was met, false on timeout.
    public bool WaitForSave(int expectedMinSaves = 1, int timeoutMs = 2000)
    {
        var start = Environment.TickCount;
        while (Volatile.Read(ref _saveCount) < expectedMinSaves)
        {
            if (Environment.TickCount - start > timeoutMs)
            {
                return false;
            }

            Thread.Sleep(50);
        }

        return true;
    }
}

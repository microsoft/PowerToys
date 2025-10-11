// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

internal sealed class MockBookmarkDataSource : IBookmarkDataSource
{
    private string _jsonData;

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
    }
}

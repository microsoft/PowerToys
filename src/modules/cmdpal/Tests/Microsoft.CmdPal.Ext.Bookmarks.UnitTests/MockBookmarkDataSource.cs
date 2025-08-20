// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

internal sealed class MockBookmarkDataSource : IBookmarkDataSource
{
    private string _jsonData;

    public MockBookmarkDataSource(string initialJsonData = "[]")
    {
        _jsonData = initialJsonData;
    }

    public string GetBookmarkDataAsync()
    {
        return _jsonData;
    }

    public void SaveBookmarkDataAsync(string jsonData)
    {
        _jsonData = jsonData;
    }
}

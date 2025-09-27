// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.CmdPal.Ext.Bookmarks.Persistence;

public sealed partial class FileBookmarkDataSource : IBookmarkDataSource
{
    private readonly string _filePath;

    public FileBookmarkDataSource(string filePath)
    {
        _filePath = filePath;
    }

    public string GetBookmarkData()
    {
        if (!File.Exists(_filePath))
        {
            return string.Empty;
        }

        try
        {
            return File.ReadAllText(_filePath);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Read bookmark data failed. ex: {ex.Message}");
            return string.Empty;
        }
    }

    public void SaveBookmarkData(string jsonData)
    {
        try
        {
            File.WriteAllText(_filePath, jsonData);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Failed to save bookmark data: {ex}");
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public class FileBookmarkDataSource : IBookmarkDataSource
{
    private readonly string _filePath;

    public FileBookmarkDataSource(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<string> GetBookmarkDataAsync()
    {
        if (!File.Exists(_filePath))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(_filePath);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Read bookmark data failed. ex: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task SaveBookmarkDataAsync(string jsonData)
    {
        try
        {
            await File.WriteAllTextAsync(_filePath, jsonData);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Failed to save bookmark data: {ex}");
        }
    }
}

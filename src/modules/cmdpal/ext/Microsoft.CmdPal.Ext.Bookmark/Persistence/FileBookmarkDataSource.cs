// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.CmdPal.Ext.Bookmarks.Persistence;

public sealed partial class FileBookmarkDataSource : IBookmarkDataSource
{
    private static readonly Lock WriteLock = new();
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
        WriteAtomically(_filePath, jsonData);
    }

    internal static void WriteAtomically(string path, string content, string? protectedPath = null)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));

            // Serialize replacements so export's store protection cannot block a normal save.
            lock (WriteLock)
            {
                using var protectedFile = protectedPath is not null && File.Exists(protectedPath)
                    ? File.OpenHandle(protectedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    : null;
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

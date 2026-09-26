// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// A file stream bound to the same validated handle for metadata checks and I/O.
/// </summary>
public sealed class SecureFile : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly FileStream stream;

    internal SecureFile(SafeFileHandle handle, FileAccess access, string finalPath)
    {
        stream = new FileStream(handle, access, bufferSize: 4096, isAsync: false);
        FinalPath = finalPath;
    }

    /// <summary>
    /// Gets the canonical path captured from this file handle.
    /// </summary>
    public string FinalPath { get; }

    /// <summary>
    /// Gets current metadata from this file handle.
    /// </summary>
    public FileHandleMetadata Metadata => SecureDirectoryRoot.GetMetadata(stream.SafeFileHandle);

    /// <summary>
    /// Reads all UTF-8 text from this handle without reopening its path.
    /// </summary>
    public string ReadAllText()
    {
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Rejects reparse points and hard links, then truncates and writes through this same handle.
    /// </summary>
    public void OverwriteAllText(string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        FileHandleMetadata metadata = Metadata;
        if (metadata.IsReparsePoint || metadata.IsDirectory || metadata.LinkCount != 1)
        {
            throw new IOException("The opened target is not a single-link regular file; overwrite rejected before truncation.");
        }

        stream.Position = 0;
        stream.SetLength(0);
        using StreamWriter writer = new(stream, Utf8WithoutBom, bufferSize: 4096, leaveOpen: true);
        writer.Write(contents);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Copies content to a newly created file through its existing handle.
    /// </summary>
    public void CopyFrom(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        stream.Position = 0;
        source.CopyTo(stream);
        stream.Flush(flushToDisk: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        stream.Dispose();
    }

    internal Stream Stream => stream;
}

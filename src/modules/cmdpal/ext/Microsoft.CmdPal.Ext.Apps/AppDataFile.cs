// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Microsoft.Extensions.Logging;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>Atomically replaces independently persisted Apps data files.</summary>
internal static partial class AppDataFile
{
    private const int ReadAttempts = 3;
    private const int ReadRetryDelayMs = 25;

    /// <summary>Reads a data file with bounded retries for I/O errors; only a missing file returns null.</summary>
    /// <returns>The file contents, or <see langword="null"/> when the file or its directory is missing.</returns>
    /// <remarks>Other failures propagate to the store so unreadable data is not replaced with empty state.</remarks>
    internal static string? ReadTextOrNull(string path, MEL.ILogger logger)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException ex) when (attempt < ReadAttempts)
            {
                LogDataFileReadRetry(logger, ex);
                Thread.Sleep(ReadRetryDelayMs);
            }
        }
    }

    /// <summary>Serializes with explicit JSON metadata and replaces the destination through a temporary file.</summary>
    /// <typeparam name="T">The persisted data contract.</typeparam>
    /// <remarks>Serialization and replacement failures propagate; temporary-file cleanup failures are logged.</remarks>
    internal static void WriteJsonAtomically<T>(string path, T content, JsonTypeInfo<T> jsonTypeInfo, MEL.ILogger logger)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(content, jsonTypeInfo));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogTemporarySettingsFileRemovalFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to remove a temporary Apps data file.")]
    private static partial void LogTemporarySettingsFileRemovalFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Retrying Apps data file read after an I/O failure.")]
    private static partial void LogDataFileReadRetry(MEL.ILogger logger, Exception exception);
}

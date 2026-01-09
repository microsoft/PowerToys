// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Models.ResizeResults;

/// <summary>
/// Represents the result of a failed replacement of the original file by the new resized image.
/// </summary>
/// <remarks>Occurs when Overwrite is enabled and there was an issue replacing the original file
/// with its resized copy.</remarks>
/// <param name="ResizedFilePath">The path to the new resized image, intended to overwrite
/// <paramref name="OriginalFilePath"/>, but failed.</param>
/// <param name="OriginalFilePath">The path to the original file that was intended to be replaced.
/// </param>
/// <param name="BackupFilePath">The path to the backup file created during the replacement
/// attempt.</param>
/// <param name="Exception">The exception that was thrown during the file replacement operation,
/// indicating the reason for failure.</param>
public record FileReplaceFailedResult(
    string ResizedFilePath,
    string OriginalFilePath,
    string BackupFilePath,
    System.Exception Exception) : ResizeResult(OriginalFilePath);

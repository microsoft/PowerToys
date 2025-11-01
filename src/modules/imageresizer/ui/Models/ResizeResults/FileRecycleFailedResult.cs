// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Models.ResizeResults;

/// <summary>
/// Represents the result of a failed attempt to delete the backup file to the Recycle Bin.
/// <remarks>At this point, <paramref name="FilePath"/> represents the resized file, as it will
/// have been just replaced. The backup file is the renamed original image file. This means both
/// resized image and backup original images are retained in the original folder.</remarks>
/// </summary>
/// <param name="FilePath">The original file path, now the location of the resized image file.
/// </param>
/// <param name="BackupFilePath">The path to the original file's backup. The failed attempt to
/// recycle this file caused the error.</param>
/// <param name="Exception">The exception that occurred during the recycle operation, providing
/// details about the failure.</param>
public record FileRecycleFailedResult(
    string FilePath,
    string BackupFilePath,
    System.Exception Exception) : ResizeResult(FilePath);

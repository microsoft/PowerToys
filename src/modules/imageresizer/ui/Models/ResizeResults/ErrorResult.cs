// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Models.ResizeResults;

/// <summary>
/// Represents an error which prevented the image resizing operation itself from completing. No
/// interim files or the destination file will have been produced. The original file is unchanged.
/// </summary>
/// <param name="FilePath">The full path to the original image file.</param>
/// <param name="Exception">The <see cref="System.Exception"/> raised during the resize operation.
/// </param>
public record ErrorResult(string FilePath, System.Exception Exception) : ResizeResult(FilePath);

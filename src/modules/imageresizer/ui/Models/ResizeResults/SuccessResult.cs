// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Models.ResizeResults;

/// <summary>
/// Represents a successful resize operation.
/// </summary>
/// <param name="FilePath">Path to the original image file.</param>
public record SuccessResult(string FilePath) : ResizeResult(FilePath);

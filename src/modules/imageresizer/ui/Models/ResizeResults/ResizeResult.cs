// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Models.ResizeResults;

/// <summary>
/// Represents the result of a resize operation, encapsulating the file path of the original image.
/// </summary>
/// <remarks>This record serves as a base for specific resize result types, providing a common
/// property for accessing the original file path.</remarks>
/// <param name="FilePath">The file path of the original image.</param>
public abstract record ResizeResult(string FilePath);

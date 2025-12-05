// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.Common.Helpers;

/// <summary>
/// Provides precomputed normalized fuzzy match targets for a title and subtitle.
/// Consumers can use these for efficient fuzzy matching operations.
/// Null values indicate that normalization was not performed or not applicable.
/// </summary>
public interface INormalizedTitles
{
    /// <summary>
    /// Gets the normalized fuzzy match target for the title, or null if not available.
    /// </summary>
    FuzzyMatchTarget NormalizedTitle { get; }

    /// <summary>
    /// Gets the normalized fuzzy match target for the subtitle, or null if not available.
    /// </summary>
    FuzzyMatchTarget NormalizedSubtitle { get; }
}

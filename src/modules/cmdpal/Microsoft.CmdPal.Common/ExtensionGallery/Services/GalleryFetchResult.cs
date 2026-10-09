// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.ExtensionGallery.Models;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

public sealed record GalleryFetchResult
{
    /// <summary>
    /// Gets or sets the gallery entries returned by the fetch operation.
    /// </summary>
    public List<GalleryExtensionEntry> Extensions { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the result was loaded from cache.
    /// </summary>
    public bool FromCache { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the service had to fall back to cached data
    /// because a remote refresh could not be completed successfully.
    /// </summary>
    public bool UsedFallbackCache { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the fetch failed because the gallery responded
    /// with HTTP 429 Too Many Requests and no cached fallback data was available.
    /// </summary>
    public bool IsRateLimited { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the fetch operation completed with an error.
    /// </summary>
    public bool HasError { get; init; }

    /// <summary>
    /// Gets or sets the error message associated with the fetch operation, when available.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

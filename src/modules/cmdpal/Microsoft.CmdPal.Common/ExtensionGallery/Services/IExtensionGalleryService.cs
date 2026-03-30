// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.ExtensionGallery.Models;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

public interface IExtensionGalleryService
{
    /// <summary>
    /// Fetches the gallery feed and extension manifests.
    /// Falls back to cached data on failure.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the fetch operation.</param>
    /// <returns>The fetched gallery data, optionally populated from cache.</returns>
    Task<GalleryFetchResult> FetchExtensionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to fetch fresh data from the feed.
    /// Falls back to cached data if the refresh fails.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the refresh operation.</param>
    /// <returns>The refreshed gallery data, optionally populated from cache.</returns>
    Task<GalleryFetchResult> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured gallery feed URL.
    /// For compatibility this method keeps its historical name, but it returns the full feed endpoint.
    /// </summary>
    /// <returns>The configured gallery feed endpoint.</returns>
    string GetBaseUrl();

    /// <summary>
    /// Returns true if a custom (non-default) feed URL is configured.
    /// </summary>
    bool IsCustomFeed { get; }

    /// <summary>
    /// Constructs the full icon URL for an extension.
    /// Returns null when the feed URL or icon path is invalid.
    /// </summary>
    /// <param name="extensionId">The gallery extension id.</param>
    /// <param name="iconFilename">The icon file name or relative path from the manifest.</param>
    /// <returns>The resolved icon URL, or null when it cannot be resolved.</returns>
    string? GetIconUrl(string extensionId, string iconFilename);
}

public sealed class GalleryFetchResult
{
    /// <summary>
    /// Gets or sets the gallery entries returned by the fetch operation.
    /// </summary>
    public List<GalleryExtensionEntry> Extensions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the result was loaded from cache.
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fetch operation completed with an error.
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    /// Gets or sets the error message associated with the fetch operation, when available.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

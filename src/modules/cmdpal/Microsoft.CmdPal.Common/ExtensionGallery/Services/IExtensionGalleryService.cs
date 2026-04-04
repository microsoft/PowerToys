// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

public interface IExtensionGalleryService
{
    /// <summary>
    /// Fetches the gallery feed.
    /// Falls back to cached data on failure.
    /// Returned entries are normalized for local display, including icon URIs.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the fetch operation.</param>
    /// <returns>The fetched gallery data, optionally populated from cache.</returns>
    Task<GalleryFetchResult> FetchExtensionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to fetch fresh data from the feed.
    /// Falls back to cached data if the refresh fails.
    /// Returned entries are normalized for local display, including icon URIs.
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
}

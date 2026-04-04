// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

/// <summary>
/// Identifies the HTTP cache instance used by the extension gallery.
/// </summary>
public sealed partial class ExtensionGalleryHttpCache : IDisposable
{
    internal const string CacheDirectoryName = "GalleryCache";
    private const int TimeoutSeconds = 15;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    internal static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromHours(4);

    public ExtensionGalleryHttpCache(IApplicationInfoService applicationInfoService, ILogger<ExtensionGalleryHttpCache> logger)
        : this(
            Path.Combine(applicationInfoService.CacheDirectory, CacheDirectoryName),
            httpClient: null,
            logger)
    {
        ArgumentNullException.ThrowIfNull(applicationInfoService);
    }

    internal ExtensionGalleryHttpCache(string cacheDirectory, HttpClient? httpClient, ILogger<ExtensionGalleryHttpCache> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys-CmdPal/1.0");
        _ownsHttpClient = httpClient is null;

        Cache = new HttpResourceCache(_httpClient, cacheDirectory, DefaultTimeToLive, logger);
    }

    internal HttpResourceCache Cache { get; }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.Services.HttpCaching;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Services;

/// <summary>
/// Identifies the HTTP client instance used by the extension gallery.
/// </summary>
public sealed partial class ExtensionGalleryHttpClient : IDisposable
{
    internal const string CacheDirectoryName = "GalleryCache";
    private const int TimeoutSeconds = 15;
    private const string UserAgent = "PowerToys-CmdPal/1.0";
    private readonly HttpCachingClient _cache;

    internal static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromHours(4);

    public ExtensionGalleryHttpClient(IApplicationInfoService applicationInfoService, ILogger<ExtensionGalleryHttpClient> logger)
        : this(applicationInfoService, innerHandler: null, logger)
    {
    }

    internal ExtensionGalleryHttpClient(IApplicationInfoService applicationInfoService, HttpMessageHandler? innerHandler, ILogger<ExtensionGalleryHttpClient> logger)
        : this(
            Path.Combine(applicationInfoService.CacheDirectory, CacheDirectoryName),
            innerHandler,
            logger)
    {
        ArgumentNullException.ThrowIfNull(applicationInfoService);
    }

    internal ExtensionGalleryHttpClient(string cacheDirectory, HttpMessageHandler? innerHandler, ILogger<ExtensionGalleryHttpClient> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = new HttpCachingClient(
            cacheDirectory,
            DefaultTimeToLive,
            TimeSpan.FromSeconds(TimeoutSeconds),
            UserAgent,
            innerHandler,
            logger);
    }

    internal HttpCachingClient Cache => _cache;

    public void Dispose()
    {
        _cache.Dispose();
    }
}

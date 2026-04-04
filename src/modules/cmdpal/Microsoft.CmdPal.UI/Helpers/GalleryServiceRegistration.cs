// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.ExtensionGallery.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class GalleryServiceRegistration
{
    /// <summary>
    /// Registers the extension gallery service, wired to the current gallery feed URL from settings.
    /// Custom feed URLs are only honored in non-CI (local dev) builds.
    /// </summary>
    public static IServiceCollection AddGalleryServices(this IServiceCollection services)
    {
        services.AddSingleton<ExtensionGalleryHttpCache>();

        services.AddSingleton<IExtensionGalleryService>(sp =>
        {
            var settingsService = sp.GetRequiredService<ISettingsService>();
            var galleryHttpCache = sp.GetRequiredService<ExtensionGalleryHttpCache>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExtensionGalleryService>>();

            // Only allow custom feed overrides in local dev builds
            Func<string?> feedUrlProvider = BuildInfo.IsCiBuild
                ? () => null
                : () => settingsService.Settings.GalleryFeedUrl;

            return new ExtensionGalleryService(galleryHttpCache, logger, feedUrlProvider);
        });

        return services;
    }
}

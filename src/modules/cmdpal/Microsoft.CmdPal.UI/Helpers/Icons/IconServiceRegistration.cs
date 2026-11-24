// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class IconServiceRegistration
{
    /*
      Cached icon-source capacities and raw BGRA estimates for a completely
      bitmap-backed Other partition:
      | Icon size | Glyph capacity | Other capacity | Other @ 100% | Other @ 200% | Other @ 300% |
      | --------- | -------------: | -------------: | -----------: | -----------: | -----------: |
      | 20×20     |           4096 |           1024 |       1.6 MB |       6.4 MB |      14.4 MB |
      | 64×64     |           1024 |            256 |       4.0 MB |      16.0 MB |      36.0 MB |
      | 256×256   |            256 |             64 |      16.0 MB |      64.0 MB |       144 MB |

      Other capacities preserve the previous shared-cache limits. Glyph entries
      retain no decoded pixel buffer, so their initial capacity is four times larger.
      This is a heuristic rather than a memory equivalence; use the per-partition
      diagnostics to tune occupancy and eviction rates from real workloads.
    */

    public static IServiceCollection AddIconServices(this IServiceCollection services, DispatcherQueue dispatcherQueue)
    {
        // Single shared loader
        var loader = new IconLoaderService(dispatcherQueue);
        services.AddSingleton<IIconLoaderService>(loader);

        // Keyed providers by size
        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size16,
            (_, _) => new IconSourceProvider(loader, 16));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size20,
            (_, _) => new CachedIconSourceProvider(
                loader,
                20,
                glyphCacheSize: 4096,
                otherCacheSize: 1024));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size32,
            (_, _) => new IconSourceProvider(loader, 32));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size64,
            (_, _) => new CachedIconSourceProvider(
                loader,
                64,
                glyphCacheSize: 1024,
                otherCacheSize: 256));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Size256,
            (_, _) => new CachedIconSourceProvider(
                loader,
                256,
                glyphCacheSize: 256,
                otherCacheSize: 64));

        services.AddKeyedSingleton<IIconSourceProvider>(
            WellKnownIconSize.Unbound,
            (_, _) => new IconSourceProvider(loader, IconLoaderService.NoResize, isPriority: true));

        return services;
    }
}

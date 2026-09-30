// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Shares cheap raw-request to Shell-identity aliases across every icon size provider.
/// Materialized icon sources remain in each provider's independently sized cache.
/// </summary>
internal sealed class ShellIconLocationCache
{
    private const int DefaultCapacity = 8192;

    private AdaptiveCache<string, LocatedShellIcon> _cache = CreateCache();
    private int _generation;

    public int Generation => Volatile.Read(ref _generation);

    public bool TryGet(string cacheIdentity, out LocatedShellIcon locatedIcon)
    {
        var generation = Generation;
        var cache = Volatile.Read(ref _cache);
        if (cache.TryGet(cacheIdentity, out locatedIcon)
            && locatedIcon.Identity.CacheGeneration == generation
            && generation == Generation
            && ReferenceEquals(cache, Volatile.Read(ref _cache)))
        {
            return true;
        }

        locatedIcon = default;
        return false;
    }

    public bool TryGet(ShellItemIconRequest request, out LocatedShellIcon locatedIcon) =>
        TryGet(request.CacheIdentity, out locatedIcon);

    public bool TryAdd(
        ShellItemIconRequest request,
        LocatedShellIcon locatedIcon,
        int expectedGeneration,
        out LocatedShellIcon cachedLocation)
    {
        cachedLocation = locatedIcon with
        {
            Identity = locatedIcon.Identity.WithCacheGeneration(expectedGeneration),
        };

        if (expectedGeneration != Generation)
        {
            cachedLocation = default;
            return false;
        }

        var cache = Volatile.Read(ref _cache);
        cache.Add(request.CacheIdentity, cachedLocation);
        if (expectedGeneration == Generation
            && ReferenceEquals(cache, Volatile.Read(ref _cache)))
        {
            return true;
        }

        // Clear raced the add. The generation on the value makes the stale alias
        // unreadable even if it landed in the replacement cache; let the caller
        // resolve the Shell identity again against the new image list.
        cachedLocation = default;
        return false;
    }

    public bool IsCurrent(LocatedShellIcon locatedIcon) =>
        locatedIcon.Identity.CacheGeneration == Generation;

    public void Clear()
    {
        // Replace the map instead of enumerating it on the window thread. The generation
        // also keeps materialized icon-cache keys obtained from an older system image list
        // from aliasing a different icon if the Shell renumbers that list.
        Interlocked.Increment(ref _generation);
        Interlocked.Exchange(ref _cache, CreateCache());
    }

    private static AdaptiveCache<string, LocatedShellIcon> CreateCache() =>
        new(DefaultCapacity, TimeSpan.FromMinutes(60));
}

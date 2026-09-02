// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Resolves a Shell request against a stable location-cache generation.
/// </summary>
internal sealed class ShellIconLocationResolver
{
    private const int MaximumAttempts = 3;

    private readonly IShellItemIconLocator _locator;
    private readonly ShellIconLocationCache _locations;
    private int _failureLogged;

    public ShellIconLocationResolver(
        IShellItemIconLocator locator,
        ShellIconLocationCache locations)
    {
        _locator = locator;
        _locations = locations;
    }

    public LocatedShellIcon? GetCurrentOrCached(
        ShellItemIconRequest request,
        LocatedShellIcon? suppliedLocation)
    {
        if (suppliedLocation is { } supplied && _locations.IsCurrent(supplied))
        {
            return supplied;
        }

        return _locations.TryGet(request, out var cachedLocation)
            ? cachedLocation
            : null;
    }

    public LocatedShellIcon Resolve(ShellItemIconRequest request)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var generation = _locations.Generation;
            var resolvedIcon = ResolveOnce(request);
            if (!resolvedIcon.CacheRawRequestAlias)
            {
                // Preserve the generation that produced the image-list index without
                // retaining a synthetic type lookup under the raw path.
                if (generation == _locations.Generation)
                {
                    return resolvedIcon with
                    {
                        Identity = resolvedIcon.Identity.WithCacheGeneration(generation),
                    };
                }

                continue;
            }

            if (_locations.TryAdd(request, resolvedIcon, generation, out var currentLocation))
            {
                return currentLocation;
            }
        }

        // Association changes can race continuously during an installer burst. Preserve
        // progress after bounded retries without sharing an image-list index that was not
        // proven against one stable generation.
        return new LocatedShellIcon(
            request,
            ShellIconIdentity.FromItemPath(
                request.ItemPath,
                request.Jumbo,
                _locations.Generation));
    }

    private LocatedShellIcon ResolveOnce(ShellItemIconRequest request)
    {
        try
        {
            if (_locator.TryLocate(request, out var resolvedIcon))
            {
                return resolvedIcon;
            }
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _failureLogged, 1) == 0)
            {
                Logger.LogError(
                    "Shell icon identity resolution failed; using path-specific caching",
                    ex);
            }
        }

        return new LocatedShellIcon(
            request,
            ShellIconIdentity.FromItemPath(request.ItemPath, request.Jumbo));
    }
}

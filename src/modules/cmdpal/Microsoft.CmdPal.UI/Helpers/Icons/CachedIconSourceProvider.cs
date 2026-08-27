// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class CachedIconSourceProvider : IIconSourceProvider
{
    private static readonly ConditionalWeakTable<IRandomAccessStreamReference, StreamIdentity> StreamIdentities = new();

    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _glyphCache;
    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _otherCache;
    private readonly ConcurrentDictionary<IconCacheKey, InFlightIconLoad> _inFlight = new();
    private readonly Size _iconSize;
    private readonly int _glyphCacheSize;
    private readonly int _otherCacheSize;
    private readonly IIconLoaderService _loader;

    public CachedIconSourceProvider(
        IIconLoaderService loader,
        Size iconSize,
        int glyphCacheSize,
        int otherCacheSize)
    {
        _loader = loader;
        _iconSize = iconSize;
        _glyphCacheSize = glyphCacheSize;
        _otherCacheSize = otherCacheSize;
        _glyphCache = new AdaptiveCache<IconCacheKey, Task<IconSource?>>(
            glyphCacheSize,
            TimeSpan.FromMinutes(60),
            removalCallback: OnGlyphCacheEntryRemoved);
        _otherCache = new AdaptiveCache<IconCacheKey, Task<IconSource?>>(
            otherCacheSize,
            TimeSpan.FromMinutes(60),
            removalCallback: OnOtherCacheEntryRemoved);
    }

    public CachedIconSourceProvider(
        IIconLoaderService loader,
        int iconSize,
        int glyphCacheSize,
        int otherCacheSize)
        : this(loader, new Size(iconSize, iconSize), glyphCacheSize, otherCacheSize)
    {
    }

    public Task<IconSource?> GetIconSource(
        IconDataViewModel icon,
        double scale,
        IconRequestMeasurement diagnostics = default,
        IIconRequestDemand? demand = null,
        ElementTheme theme = ElementTheme.Default)
    {
        if (icon.Icon is { } iconString)
        {
            var isExplicitShellRequest =
                Microsoft.CommandPalette.Extensions.Toolkit.ShellItemIconProtocol.IsProtocol(iconString);
            if (icon.Data?.Unsafe is null || isExplicitShellRequest)
            {
                if (isExplicitShellRequest
                    || iconString.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                {
                    if (_loader.ShellIconLocations.TryGet(iconString, out var cachedLocation))
                    {
                        var shellDiagnostics = IconLoadDiagnostics.BeginShellIconRequest(cachedLocation.Request);
                        shellDiagnostics.LocationCacheHit();
                        return GetShellItemIconSource(
                            cachedLocation.Request,
                            icon,
                            scale,
                            diagnostics,
                            demand,
                            shellDiagnostics,
                            cachedLocation);
                    }

                    if (ShellItemIconRequestClassifier.TryClassify(iconString, out var uncachedRequest))
                    {
                        var shellDiagnostics = IconLoadDiagnostics.BeginShellIconRequest(uncachedRequest);
                        shellDiagnostics.LocationCacheMiss();
                        return GetShellItemIconSource(
                            uncachedRequest,
                            icon,
                            scale,
                            diagnostics,
                            demand,
                            shellDiagnostics,
                            locationAlreadyChecked: true);
                    }
                }
                else if (ShellItemIconRequestClassifier.TryClassify(iconString, out var shellRequest))
                {
                    var shellDiagnostics = IconLoadDiagnostics.BeginShellIconRequest(shellRequest);
                    return GetShellItemIconSource(
                        shellRequest,
                        icon,
                        scale,
                        diagnostics,
                        demand,
                        shellDiagnostics);
                }
            }
        }

        var protocolProcessor = IconProtocolRegistry.Find(icon.Icon);
        var iconIdentity = icon.Icon is { } cacheIconString && protocolProcessor is not null
            ? protocolProcessor.GetCacheIdentity(cacheIconString)
            : icon.Icon;
        var cacheTheme = protocolProcessor?.GetCacheTheme(icon.Icon!, theme) ?? ElementTheme.Default;
        var key = new IconCacheKey(icon, iconIdentity, scale, cacheTheme);
        var partition = ClassifyCachePartition(icon.Icon, protocolProcessor);
        var cache = GetCache(partition);
        var cacheSize = GetCacheSize(partition);

        if (cache.TryGet(key, out var existingTask))
        {
            IconLoadDiagnostics.RecordCacheLookup(_iconSize, partition, cacheSize, hit: true);
            diagnostics.RecordProviderResolution(IconProviderResolution.CacheHit, existingTask);
            return existingTask;
        }

        IconLoadDiagnostics.RecordCacheLookup(_iconSize, partition, cacheSize, hit: false);
        return GetOrCreateSlowPath(key, icon, scale, theme, partition, diagnostics, demand);
    }

    private Task<IconSource?> GetShellItemIconSource(
        ShellItemIconRequest request,
        IconDataViewModel icon,
        double scale,
        IconRequestMeasurement diagnostics,
        IIconRequestDemand? demand,
        ShellIconMeasurement shellDiagnostics,
        LocatedShellIcon? knownLocation = null,
        bool locationAlreadyChecked = false)
    {
        var locatedIcon = knownLocation;
        if (locatedIcon is null && !locationAlreadyChecked)
        {
            if (_loader.ShellIconLocations.TryGet(request, out var cachedLocation))
            {
                shellDiagnostics.LocationCacheHit();
                locatedIcon = cachedLocation;
            }
            else
            {
                shellDiagnostics.LocationCacheMiss();
            }
        }

        if (locatedIcon is { } canonicalLocation)
        {
            return GetOrCreateLocatedShellItemLoad(
                request,
                canonicalLocation,
                icon,
                scale,
                diagnostics,
                demand,
                shellDiagnostics);
        }

        IconLoadDiagnostics.RecordCacheLookup(
            _iconSize,
            IconCachePartition.Other,
            _otherCacheSize,
            hit: false);

        // Before Shell localization, only identical raw requests can share work. Do not
        // cache this key: once resolved, the canonical Shell identity owns the entry.
        var rawKey = new IconCacheKey(icon, icon.Icon, scale, ElementTheme.Default);
        var candidate = new InFlightIconLoad();
        var pending = _inFlight.GetOrAdd(rawKey, candidate);
        if (!ReferenceEquals(pending, candidate))
        {
            shellDiagnostics.RawInFlightJoin();
            pending.Demand.Attach(demand);
            diagnostics.RecordProviderResolution(IconProviderResolution.InFlight, pending.Task);
            return pending.Task;
        }

        ObserveInFlightRemoval(rawKey, pending);

        IconLoadMeasurement? loadDiagnostics = null;
        try
        {
            loadDiagnostics = CreateLoadDiagnostics(icon, scale, diagnostics, pending.Task);
            pending.Demand.Attach(demand);
            var coordinator = new ShellItemIconLoadCoordinator(
                this,
                pending,
                scale,
                shellDiagnostics);
            if (!_loader.TryEnqueueShellItemLoad(
                    request,
                    locatedIcon: null,
                    _iconSize,
                    scale,
                    pending,
                    IconLoadPriority.Low,
                    loadDiagnostics,
                    pending.Demand,
                    coordinator,
                    shellDiagnostics))
            {
                pending.TrySetException(new ObjectDisposedException(nameof(IIconLoaderService)));
            }
        }
        catch (Exception ex)
        {
            loadDiagnostics?.Rejected();
            pending.TrySetException(ex);
        }

        return pending.Task;
    }

    private Task<IconSource?> GetOrCreateLocatedShellItemLoad(
        ShellItemIconRequest request,
        LocatedShellIcon locatedIcon,
        IconDataViewModel icon,
        double scale,
        IconRequestMeasurement diagnostics,
        IIconRequestDemand? demand,
        ShellIconMeasurement shellDiagnostics)
    {
        var candidate = new InFlightIconLoad();
        var arbitration = ArbitrateCanonicalShellLoad(
            locatedIcon,
            scale,
            candidate,
            shellDiagnostics);
        if (arbitration.Kind == CanonicalShellLoadArbitrationKind.CacheHit)
        {
            diagnostics.RecordProviderResolution(IconProviderResolution.CacheHit, arbitration.Task);
            return arbitration.Task;
        }

        var pending = arbitration.Pending!;
        if (arbitration.Kind == CanonicalShellLoadArbitrationKind.InFlightJoin)
        {
            pending.Demand.Attach(demand);
            diagnostics.RecordProviderResolution(IconProviderResolution.InFlight, pending.Task);
            return pending.Task;
        }

        IconLoadMeasurement? loadDiagnostics = null;
        try
        {
            loadDiagnostics = CreateLoadDiagnostics(icon, scale, diagnostics, pending.Task);
            pending.Demand.Attach(demand);
            if (!_loader.TryEnqueueShellItemLoad(
                    request,
                    locatedIcon,
                    _iconSize,
                    scale,
                    pending,
                    IconLoadPriority.Low,
                    loadDiagnostics,
                    pending.Demand,
                    shellDiagnostics: shellDiagnostics))
            {
                pending.TrySetException(new ObjectDisposedException(nameof(IIconLoaderService)));
            }
        }
        catch (Exception ex)
        {
            loadDiagnostics?.Rejected();
            pending.TrySetException(ex);
        }

        return pending.Task;
    }

    private Task<IconSource?> GetOrCreateSlowPath(
        IconCacheKey key,
        IconDataViewModel icon,
        double scale,
        ElementTheme theme,
        IconCachePartition partition,
        IconRequestMeasurement diagnostics,
        IIconRequestDemand? demand)
    {
        var candidate = new InFlightIconLoad();

        var pending = _inFlight.GetOrAdd(key, candidate);
        if (!ReferenceEquals(pending, candidate))
        {
            pending.Demand.Attach(demand);
            diagnostics.RecordProviderResolution(IconProviderResolution.InFlight, pending.Task);
            return pending.Task;
        }

        var tcs = pending;
        var task = pending.Task;
        IconLoadMeasurement? loadDiagnostics = null;

        ObserveCompletedLoad(key, pending, partition);

        try
        {
            var streamReference = icon.Data?.Unsafe;
            loadDiagnostics = IconLoadDiagnostics.CreateLoad(
                diagnostics,
                icon.Icon,
                streamReference is not null,
                _iconSize.Width,
                _iconSize.Height,
                scale);
            loadDiagnostics?.RegisterTask(task);
            diagnostics.RecordProviderResolution(IconProviderResolution.NewLoad, loadDiagnostics);

            if (_loader.TryLoadGlyph(icon.Icon, icon.FontFamily, _iconSize, scale, out var glyph) && glyph is not null)
            {
                loadDiagnostics?.CompleteDirectGlyph(glyph);
                tcs.TrySetResult(glyph);
                return task;
            }

            pending.Demand.Attach(demand);

            if (!_loader.TryEnqueueLoad(
                    icon.Icon,
                    icon.FontFamily,
                    streamReference,
                    _iconSize,
                    scale,
                    theme,
                    tcs,
                    IconLoadPriority.Low,
                    loadDiagnostics,
                    pending.Demand))
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(IIconLoaderService)));
            }
        }
        catch (Exception ex)
        {
            loadDiagnostics?.Rejected();
            tcs.TrySetException(ex);
        }

        return task;
    }

    private IconLoadMeasurement? CreateLoadDiagnostics(
        IconDataViewModel icon,
        double scale,
        IconRequestMeasurement diagnostics,
        Task<IconSource?> task)
    {
        var loadDiagnostics = IconLoadDiagnostics.CreateLoad(
            diagnostics,
            icon.Icon,
            hasStream: false,
            _iconSize.Width,
            _iconSize.Height,
            scale);
        loadDiagnostics?.RegisterTask(task);
        diagnostics.RecordProviderResolution(IconProviderResolution.NewLoad, loadDiagnostics);
        return loadDiagnostics;
    }

    private void ObserveCompletedLoad(
        IconCacheKey key,
        InFlightIconLoad pending,
        IconCachePartition partition,
        bool cacheFallbackResults = true)
    {
        var cache = GetCache(partition);
        var cacheSize = GetCacheSize(partition);
        _ = pending.Task.ContinueWith(
            completed =>
            {
                try
                {
                    if (completed.IsCompletedSuccessfully
                        && (cacheFallbackResults
                            || (completed.Result is { } result
                                && !ShellItemIconFallback.IsFallback(result))))
                    {
                        cache.Add(key, completed);
                        IconLoadDiagnostics.RecordCacheEntryAdded(
                            _iconSize,
                            partition,
                            cacheSize,
                            cache.ApproximateCount);
                    }
                }
                finally
                {
                    _inFlight.TryRemove(new KeyValuePair<IconCacheKey, InFlightIconLoad>(key, pending));
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void ObserveInFlightRemoval(IconCacheKey key, InFlightIconLoad pending)
    {
        _ = pending.Task.ContinueWith(
            _ => _inFlight.TryRemove(new KeyValuePair<IconCacheKey, InFlightIconLoad>(key, pending)),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private bool TryJoinLocatedShellItemLoad(
        InFlightIconLoad pending,
        double scale,
        LocatedShellIcon locatedIcon,
        ShellIconMeasurement shellDiagnostics,
        out Task<IconSource?> sharedTask)
    {
        var arbitration = ArbitrateCanonicalShellLoad(
            locatedIcon,
            scale,
            pending,
            shellDiagnostics);
        if (arbitration.Kind == CanonicalShellLoadArbitrationKind.CacheHit)
        {
            sharedTask = arbitration.Task;
            return true;
        }

        if (arbitration.Kind == CanonicalShellLoadArbitrationKind.InFlightJoin)
        {
            // Our requesters are already bound to the raw load and cannot be re-pointed,
            // so pin the canonical load demanded for the rest of its life. Demand only
            // drives ordering: over-reporting delays a demotion, while under-reporting
            // can starve a canonical load that still has a live row waiting on it.
            var canonicalPending = arbitration.Pending!;
            canonicalPending.Demand.Attach(null);
            sharedTask = canonicalPending.Task;
            return true;
        }

        sharedTask = null!;
        return false;
    }

    private CanonicalShellLoadArbitration ArbitrateCanonicalShellLoad(
        LocatedShellIcon locatedIcon,
        double scale,
        InFlightIconLoad candidate,
        ShellIconMeasurement shellDiagnostics)
    {
        var canonicalKey = new IconCacheKey(locatedIcon.Identity, scale);
        if (_otherCache.TryGet(canonicalKey, out var cachedTask))
        {
            shellDiagnostics.CanonicalCacheHit();
            IconLoadDiagnostics.RecordCacheLookup(
                _iconSize,
                IconCachePartition.Other,
                _otherCacheSize,
                hit: true);
            return new CanonicalShellLoadArbitration(
                CanonicalShellLoadArbitrationKind.CacheHit,
                null,
                cachedTask);
        }

        IconLoadDiagnostics.RecordCacheLookup(
            _iconSize,
            IconCachePartition.Other,
            _otherCacheSize,
            hit: false);
        var pending = _inFlight.GetOrAdd(canonicalKey, candidate);
        if (!ReferenceEquals(pending, candidate))
        {
            shellDiagnostics.CanonicalInFlightJoin();
            return new CanonicalShellLoadArbitration(
                CanonicalShellLoadArbitrationKind.InFlightJoin,
                pending,
                pending.Task);
        }

        shellDiagnostics.CanonicalNewLoad();
        ObserveCompletedLoad(
            canonicalKey,
            pending,
            IconCachePartition.Other,
            cacheFallbackResults: false);
        return new CanonicalShellLoadArbitration(
            CanonicalShellLoadArbitrationKind.NewLoad,
            pending,
            pending.Task);
    }

    private static IconCachePartition ClassifyCachePartition(
        string? iconString,
        IIconProtocolProcessor? protocolProcessor)
    {
        if (protocolProcessor is not null)
        {
            return protocolProcessor.CachePartition;
        }

        try
        {
            return FontIconGlyphClassifier.IsGlyphCandidate(iconString)
                ? IconCachePartition.Glyph
                : IconCachePartition.Other;
        }
        catch
        {
            // Keep routing consistent with TryLoadGlyph: classifier failures use the
            // general icon loader and therefore belong in the non-glyph cache.
            return IconCachePartition.Other;
        }
    }

    private AdaptiveCache<IconCacheKey, Task<IconSource?>> GetCache(IconCachePartition partition) =>
        partition == IconCachePartition.Glyph ? _glyphCache : _otherCache;

    private int GetCacheSize(IconCachePartition partition) =>
        partition == IconCachePartition.Glyph ? _glyphCacheSize : _otherCacheSize;

    private void OnGlyphCacheEntryRemoved(
        IconCacheKey key,
        Task<IconSource?> task,
        AdaptiveCacheRemovalReason reason,
        int remainingCount,
        int capacity) =>
        OnCacheEntryRemoved(IconCachePartition.Glyph, key, task, reason, remainingCount, capacity);

    private void OnOtherCacheEntryRemoved(
        IconCacheKey key,
        Task<IconSource?> task,
        AdaptiveCacheRemovalReason reason,
        int remainingCount,
        int capacity) =>
        OnCacheEntryRemoved(IconCachePartition.Other, key, task, reason, remainingCount, capacity);

    private void OnCacheEntryRemoved(
        IconCachePartition partition,
        IconCacheKey key,
        Task<IconSource?> task,
        AdaptiveCacheRemovalReason reason,
        int remainingCount,
        int capacity)
    {
        _ = key;
        _ = task;
        IconLoadDiagnostics.RecordCacheEntryRemoved(
            _iconSize,
            partition,
            capacity,
            remainingCount,
            reason);
    }

    private sealed class InFlightIconLoad : TaskCompletionSource<IconSource?>
    {
        private IconLoadDemand? _demand;

        public InFlightIconLoad()
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
        }

        public IconLoadDemand Demand => LazyInitializer.EnsureInitialized(ref _demand);
    }

    private enum CanonicalShellLoadArbitrationKind
    {
        CacheHit,
        InFlightJoin,
        NewLoad,
    }

    private readonly record struct CanonicalShellLoadArbitration(
        CanonicalShellLoadArbitrationKind Kind,
        InFlightIconLoad? Pending,
        Task<IconSource?> Task);

    private readonly struct IconCacheKey : IEquatable<IconCacheKey>
    {
        private readonly string? _icon;
        private readonly string? _fontFamily;
        private readonly StreamIdentity? _streamIdentity;
        private readonly ShellIconIdentity? _shellIdentity;
        private readonly int _scale;
        private readonly ElementTheme _theme;

        public IconCacheKey(
            IconDataViewModel icon,
            string? iconIdentity,
            double scale,
            ElementTheme cacheTheme)
        {
            _icon = iconIdentity;
            _fontFamily = icon.FontFamily;
            _streamIdentity = icon.Data?.Unsafe is { } stream
                ? StreamIdentities.GetValue(stream, static _ => new StreamIdentity())
                : null;
            _shellIdentity = null;
            _scale = (int)(100 * Math.Round(scale, 2));
            _theme = cacheTheme;
        }

        public IconCacheKey(ShellIconIdentity shellIdentity, double scale)
        {
            _icon = null;
            _fontFamily = null;
            _streamIdentity = null;
            _shellIdentity = shellIdentity;
            _scale = (int)(100 * Math.Round(scale, 2));
            _theme = ElementTheme.Default;
        }

        public bool Equals(IconCacheKey other) =>
            _icon == other._icon &&
            _fontFamily == other._fontFamily &&
            ReferenceEquals(_streamIdentity, other._streamIdentity) &&
            _shellIdentity == other._shellIdentity &&
            _scale == other._scale &&
            _theme == other._theme;

        public override bool Equals(object? obj) => obj is IconCacheKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(_icon, _fontFamily, _streamIdentity, _shellIdentity, _scale, _theme);
    }

    private sealed class ShellItemIconLoadCoordinator : IShellItemIconLoadCoordinator
    {
        private readonly CachedIconSourceProvider _owner;
        private readonly InFlightIconLoad _pending;
        private readonly double _scale;
        private readonly ShellIconMeasurement _shellDiagnostics;

        public ShellItemIconLoadCoordinator(
            CachedIconSourceProvider owner,
            InFlightIconLoad pending,
            double scale,
            ShellIconMeasurement shellDiagnostics)
        {
            _owner = owner;
            _pending = pending;
            _scale = scale;
            _shellDiagnostics = shellDiagnostics;
        }

        public bool TryJoinExistingLoad(
            LocatedShellIcon locatedIcon,
            out Task<IconSource?> sharedTask) =>
            _owner.TryJoinLocatedShellItemLoad(
                _pending,
                _scale,
                locatedIcon,
                _shellDiagnostics,
                out sharedTask);
    }

    // A RuntimeHelpers.GetHashCode value is not unique. Keep a weak mapping from each
    // stream reference to a stable token so cached keys cannot alias distinct streams,
    // without making the cache retain the stream and its encoded image data.
    private sealed class StreamIdentity
    {
    }
}

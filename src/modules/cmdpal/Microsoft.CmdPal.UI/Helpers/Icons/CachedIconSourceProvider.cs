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
        var protocolProcessor = IconProtocolRegistry.Find(icon.Icon);
        var iconIdentity = icon.Icon is { } iconString && protocolProcessor is not null
            ? protocolProcessor.GetCacheIdentity(iconString)
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
        var cache = GetCache(partition);
        var cacheSize = GetCacheSize(partition);
        IconLoadMeasurement? loadDiagnostics = null;

        _ = task.ContinueWith(
            completed =>
            {
                try
                {
                    if (completed.IsCompletedSuccessfully)
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

    private readonly struct IconCacheKey : IEquatable<IconCacheKey>
    {
        private readonly string? _icon;
        private readonly string? _fontFamily;
        private readonly StreamIdentity? _streamIdentity;
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
            _scale = (int)(100 * Math.Round(scale, 2));
            _theme = cacheTheme;
        }

        public bool Equals(IconCacheKey other) =>
            _icon == other._icon &&
            _fontFamily == other._fontFamily &&
            ReferenceEquals(_streamIdentity, other._streamIdentity) &&
            _scale == other._scale &&
            _theme == other._theme;

        public override bool Equals(object? obj) => obj is IconCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_icon, _fontFamily, _streamIdentity, _scale, _theme);
    }

    // A RuntimeHelpers.GetHashCode value is not unique. Keep a weak mapping from each
    // stream reference to a stable token so cached keys cannot alias distinct streams,
    // without making the cache retain the stream and its encoded image data.
    private sealed class StreamIdentity
    {
    }
}

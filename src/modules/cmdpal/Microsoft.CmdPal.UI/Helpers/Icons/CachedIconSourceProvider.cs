// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class CachedIconSourceProvider : IIconSourceProvider
{
    private static readonly ConditionalWeakTable<IRandomAccessStreamReference, StreamIdentity> StreamIdentities = new();

    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _cache;
    private readonly ConcurrentDictionary<IconCacheKey, Task<IconSource?>> _inFlight = new();
    private readonly Size _iconSize;
    private readonly int _cacheSize;
    private readonly IIconLoaderService _loader;

    public CachedIconSourceProvider(IIconLoaderService loader, Size iconSize, int cacheSize)
    {
        _loader = loader;
        _iconSize = iconSize;
        _cacheSize = cacheSize;
        _cache = new AdaptiveCache<IconCacheKey, Task<IconSource?>>(
            cacheSize,
            TimeSpan.FromMinutes(60),
            removalCallback: OnCacheEntryRemoved);
    }

    public CachedIconSourceProvider(IIconLoaderService loader, int iconSize, int cacheSize)
        : this(loader, new Size(iconSize, iconSize), cacheSize)
    {
    }

    public Task<IconSource?> GetIconSource(IconDataViewModel icon, double scale, IconRequestMeasurement diagnostics = default)
    {
        var key = new IconCacheKey(icon, scale);

        if (_cache.TryGet(key, out var existingTask))
        {
            IconLoadDiagnostics.RecordCacheLookup(_iconSize, _cacheSize, hit: true);
            diagnostics.RecordProviderResolution(IconProviderResolution.CacheHit, existingTask);
            return existingTask;
        }

        IconLoadDiagnostics.RecordCacheLookup(_iconSize, _cacheSize, hit: false);
        return GetOrCreateSlowPath(key, icon, scale, diagnostics);
    }

    private Task<IconSource?> GetOrCreateSlowPath(
        IconCacheKey key,
        IconDataViewModel icon,
        double scale,
        IconRequestMeasurement diagnostics)
    {
        var tcs = new TaskCompletionSource<IconSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = tcs.Task;

        var pending = _inFlight.GetOrAdd(key, task);
        if (!ReferenceEquals(pending, task))
        {
            diagnostics.RecordProviderResolution(IconProviderResolution.InFlight, pending);
            return pending;
        }

        IconLoadMeasurement? loadDiagnostics = null;

        _ = task.ContinueWith(
            completed =>
            {
                try
                {
                    if (completed.IsCompletedSuccessfully)
                    {
                        _cache.Add(key, completed);
                        IconLoadDiagnostics.RecordCacheEntryAdded(
                            _iconSize,
                            _cacheSize,
                            _cache.ApproximateCount);
                    }
                }
                finally
                {
                    _inFlight.TryRemove(new KeyValuePair<IconCacheKey, Task<IconSource?>>(key, completed));
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

            if (!_loader.TryEnqueueLoad(
                    icon.Icon,
                    icon.FontFamily,
                    streamReference,
                    _iconSize,
                    scale,
                    tcs,
                    IconLoadPriority.Low,
                    loadDiagnostics))
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

    private void OnCacheEntryRemoved(
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
            capacity,
            remainingCount,
            reason);
    }

    private readonly struct IconCacheKey : IEquatable<IconCacheKey>
    {
        private readonly string? _icon;
        private readonly string? _fontFamily;
        private readonly StreamIdentity? _streamIdentity;
        private readonly int _scale;

        public IconCacheKey(IconDataViewModel icon, double scale)
        {
            _icon = icon.Icon;
            _fontFamily = icon.FontFamily;
            _streamIdentity = icon.Data?.Unsafe is { } stream
                ? StreamIdentities.GetValue(stream, static _ => new StreamIdentity())
                : null;
            _scale = (int)(100 * Math.Round(scale, 2));
        }

        public bool Equals(IconCacheKey other) =>
            _icon == other._icon &&
            _fontFamily == other._fontFamily &&
            ReferenceEquals(_streamIdentity, other._streamIdentity) &&
            _scale == other._scale;

        public override bool Equals(object? obj) => obj is IconCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_icon, _fontFamily, _streamIdentity, _scale);
    }

    // A RuntimeHelpers.GetHashCode value is not unique. Keep a weak mapping from each
    // stream reference to a stable token so cached keys cannot alias distinct streams,
    // without making the cache retain the stream and its encoded image data.
    private sealed class StreamIdentity
    {
    }
}

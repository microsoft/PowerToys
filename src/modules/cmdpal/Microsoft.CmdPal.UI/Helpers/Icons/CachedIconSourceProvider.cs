// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class CachedIconSourceProvider : IIconSourceProvider
{
    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _cache;
    private readonly ConcurrentDictionary<IconCacheKey, Task<IconSource?>> _inFlight = new();
    private readonly Size _iconSize;
    private readonly IIconLoaderService _loader;

    public CachedIconSourceProvider(IIconLoaderService loader, Size iconSize, int cacheSize)
    {
        _loader = loader;
        _iconSize = iconSize;
        _cache = new AdaptiveCache<IconCacheKey, Task<IconSource?>>(cacheSize, TimeSpan.FromMinutes(60));
    }

    public CachedIconSourceProvider(IIconLoaderService loader, int iconSize, int cacheSize)
        : this(loader, new Size(iconSize, iconSize), cacheSize)
    {
    }

    public Task<IconSource?> GetIconSource(IconDataViewModel icon, double scale)
    {
        var key = new IconCacheKey(icon, scale);

        return _cache.TryGet(key, out var existingTask)
            ? existingTask
            : GetOrCreateSlowPath(key, icon, scale);
    }

    private Task<IconSource?> GetOrCreateSlowPath(IconCacheKey key, IconDataViewModel icon, double scale)
    {
        var tcs = new TaskCompletionSource<IconSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = tcs.Task;

        var pending = _inFlight.GetOrAdd(key, task);
        if (!ReferenceEquals(pending, task))
        {
            return pending;
        }

        _ = task.ContinueWith(
            completed =>
            {
                try
                {
                    if (completed.IsCompletedSuccessfully)
                    {
                        _cache.Add(key, completed);
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
            if (!_loader.TryEnqueueLoad(
                    icon.Icon,
                    icon.FontFamily,
                    icon.Data?.Unsafe,
                    _iconSize,
                    scale,
                    tcs,
                    IconLoadPriority.Low))
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(IIconLoaderService)));
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return task;
    }

    private readonly struct IconCacheKey : IEquatable<IconCacheKey>
    {
        private readonly string? _icon;
        private readonly string? _fontFamily;
        private readonly int _streamRefHashCode;
        private readonly int _scale;

        public IconCacheKey(IconDataViewModel icon, double scale)
        {
            _icon = icon.Icon;
            _fontFamily = icon.FontFamily;
            _streamRefHashCode = icon.Data?.Unsafe is { } stream
                ? RuntimeHelpers.GetHashCode(stream)
                : 0;
            _scale = (int)(100 * Math.Round(scale, 2));
        }

        public bool Equals(IconCacheKey other) =>
            _icon == other._icon &&
            _fontFamily == other._fontFamily &&
            _streamRefHashCode == other._streamRefHashCode &&
            _scale == other._scale;

        public override bool Equals(object? obj) => obj is IconCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_icon, _fontFamily, _streamRefHashCode, _scale);
    }
}

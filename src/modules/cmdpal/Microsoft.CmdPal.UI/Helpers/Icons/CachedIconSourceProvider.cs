// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class CachedIconSourceProvider : IIconSourceProvider
{
    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _cache;
    private readonly Size _iconSize;
    private readonly IconLoaderService _loader;
    private readonly Lock _lock = new();

    public CachedIconSourceProvider(IconLoaderService loader, Size iconSize, int cacheSize)
    {
        _loader = loader;
        _iconSize = iconSize;
        _cache = new AdaptiveCache<IconCacheKey, Task<IconSource?>>(cacheSize, TimeSpan.FromMinutes(60));
    }

    public CachedIconSourceProvider(IconLoaderService loader, int iconSize, int cacheSize)
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
        lock (_lock)
        {
            if (_cache.TryGet(key, out var existingTask))
            {
                return existingTask;
            }

            var tcs = new TaskCompletionSource<IconSource?>(TaskCreationOptions.RunContinuationsAsynchronously);

#if DEBUG
            var logMessage = $"Cache miss for icon: '{icon.Icon}', FontFamily: '{icon.FontFamily}', StreamRefHash: '{key.GetHashCode()}', Scale: {scale}";
            CoreLogger.LogInfo(logMessage);
#endif

            _loader.EnqueueLoad(
                icon.Icon,
                icon.FontFamily,
                icon.Data?.Unsafe,
                _iconSize,
                scale,
                tcs);

            var task = tcs.Task;

            _ = task.ContinueWith(
                _ =>
                {
                    lock (_lock)
                    {
                        _cache.TryRemove(key);
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);

            _cache.Add(key, task);
            return task;
        }
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

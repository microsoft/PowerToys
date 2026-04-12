// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class CachedIconSourceProvider : IIconSourceProvider
{
    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _cache;
    private readonly string _name;
    private readonly Size _iconSize;
    private readonly IconLoaderService _loader;
    private readonly Lock _lock = new();

    public CachedIconSourceProvider(IconLoaderService loader, Size iconSize, int cacheSize, string name)
    {
        _loader = loader;
        _name = name;
        _iconSize = iconSize;
        _cache = new AdaptiveCache<IconCacheKey, Task<IconSource?>>(cacheSize, TimeSpan.FromMinutes(60));
    }

    public CachedIconSourceProvider(IconLoaderService loader, int iconSize, int cacheSize)
        : this(loader, new Size(iconSize, iconSize), cacheSize, $"{iconSize}x{iconSize}")
    {
    }

    public CachedIconSourceProvider(IconLoaderService loader, int iconSize, int cacheSize, string name)
        : this(loader, new Size(iconSize, iconSize), cacheSize, name)
    {
    }

    public Task<IconSource?> GetIconSource(IconDataViewModel icon, double scale, ElementTheme theme)
    {
        var key = new IconCacheKey(icon, scale, theme);

        return _cache.TryGet(key, out var existingTask)
            ? existingTask
            : GetOrCreateSlowPath(key, icon, scale, theme);
    }

    private Task<IconSource?> GetOrCreateSlowPath(IconCacheKey key, IconDataViewModel icon, double scale, ElementTheme theme)
    {
        lock (_lock)
        {
            if (_cache.TryGet(key, out var existingTask))
            {
                return existingTask;
            }

            var tcs = new TaskCompletionSource<IconSource?>(TaskCreationOptions.RunContinuationsAsynchronously);

            _loader.EnqueueLoad(
                icon.Icon,
                icon.FontFamily,
                icon.Data?.Unsafe,
                _iconSize,
                theme,
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

    internal CachedIconSourceProviderDiagnostics GetDiagnostics()
    {
        return new CachedIconSourceProviderDiagnostics(_name, _iconSize, _cache.GetStatistics());
    }

    internal void PruneCache()
    {
        _cache.Clear();
    }

    private readonly struct IconCacheKey : IEquatable<IconCacheKey>
    {
        private readonly string? _icon;
        private readonly string? _fontFamily;
        private readonly int _streamRefHashCode;
        private readonly int _scale;
        private readonly ElementTheme _theme;

        public IconCacheKey(IconDataViewModel icon, double scale, ElementTheme theme)
        {
            _icon = icon.Icon;
            _fontFamily = icon.FontFamily;
            _streamRefHashCode = icon.Data?.Unsafe is { } stream
                ? RuntimeHelpers.GetHashCode(stream)
                : 0;
            _scale = (int)(100 * Math.Round(scale, 2));
            _theme = IconStringParser.RequiresTheme(_icon)
                ? theme
                : ElementTheme.Default;
        }

        public bool Equals(IconCacheKey other) =>
            _icon == other._icon &&
            _fontFamily == other._fontFamily &&
            _streamRefHashCode == other._streamRefHashCode &&
            _scale == other._scale &&
            _theme == other._theme;

        public override bool Equals(object? obj) => obj is IconCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_icon, _fontFamily, _streamRefHashCode, _scale, _theme);
    }
}

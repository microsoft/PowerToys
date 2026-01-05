// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.Terminal.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

public sealed class IconCacheService
{
    private const DispatcherQueuePriority LoadingPriorityOnDispatcher = DispatcherQueuePriority.Low;

    // Throttle concurrent icon loads to avoid overwhelming the system and UI thread.
    // The catch-22 is that loading icons requires some UI thread work and UI thread can block
    // itself with D2D multithread lock while delegating work to background threads to load icons.
    // Loading and decoding SVGs turned out to be evil...
    private static readonly Channel<Func<Task>> HighPriorityQueue = Channel.CreateBounded<Func<Task>>(32);
    private static readonly Channel<Func<Task>> LowPriorityQueue = Channel.CreateUnbounded<Func<Task>>();

    // Caches Task<IconSource?> so multiple requests for the same icon while loading
    // share the same task.
    private readonly AdaptiveCache<IconCacheKey, Task<IconSource?>> _cache;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Size _iconSize;
    private readonly Lock _lock = new();

    static IconCacheService()
    {
        // Start 4 global workers that live for the duration of the app
        for (var i = 0; i < 4; i++)
        {
            _ = Task.Run(ProcessSharedQueueAsync);
        }
    }

    public IconCacheService(DispatcherQueue dispatcherQueue, Size iconSize, int cacheSize)
    {
        _dispatcherQueue = dispatcherQueue;
        _iconSize = iconSize;
        _cache = new(cacheSize, TimeSpan.FromMinutes(60), decayFactor: 0.5);
    }

    public ValueTask<IconSource?> GetIconSource(IconDataViewModel icon, double scale)
    {
        var key = new IconCacheKey(icon, scale);

        // If it's in the cache, return the Task wrapped in a ValueTask.
        // No 'async/await' here = no state machine allocation
        if (_cache.TryGet(key, out var existingTask))
        {
            return new ValueTask<IconSource?>(existingTask);
        }
        else
        {
            return new ValueTask<IconSource?>(GetOrCreateSlowPath(key, icon, scale));
        }
    }

    private static async Task ProcessSharedQueueAsync()
    {
        while (true)
        {
            Func<Task>? workItem;

            // 1. Always try to drain the High Priority queue first
            if (HighPriorityQueue.Reader.TryRead(out workItem))
            {
                await ExecuteWork(workItem).ConfigureAwait(false);
                continue;
            }

            // 2. If High is empty, wait for either (using WaitToRead)
            // This is a bit tricky with two channels, so we use a Task.WhenAny
            // or a simple prioritized check loop.
            var highWait = HighPriorityQueue.Reader.WaitToReadAsync().AsTask();
            var lowWait = LowPriorityQueue.Reader.WaitToReadAsync().AsTask();

            await Task.WhenAny(highWait, lowWait).ConfigureAwait(false);

            if (HighPriorityQueue.Reader.TryRead(out workItem)
                || LowPriorityQueue.Reader.TryRead(out workItem))
            {
                await ExecuteWork(workItem).ConfigureAwait(false);
            }
        }

        static async Task ExecuteWork(Func<Task> workItem)
        {
            try
            {
                await workItem().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Swallow exceptions to keep the worker alive
                Logger.LogError("Failed to load icon", ex);
            }
        }
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

            // prevent capture of the view model
            var iconString = icon.Icon;
            var fontFamily = icon.FontFamily;
            var streamRef = icon.Data?.Unsafe;

            // Enqueue the work item to load the icon
            // Slightly ahead of its time... we have multiple queues, but for now
            // no way to prioritize individual requests, so all icon loads go to LowPriorityQueue.
            LowPriorityQueue.Writer.TryWrite(WorkItem);

            var task = tcs.Task;
            _cache.Add(key, task);
            return task;

            async Task WorkItem()
            {
                try
                {
                    var result = await LoadIconCoreAsync(iconString, fontFamily, streamRef, scale);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    lock (_lock)
                    {
                        _cache.TryRemove(key);
                    }

                    tcs.TrySetResult(null);
                    Logger.LogError("Failed to load icon", ex);
                }
            }
        }
    }

    private async Task<IconSource?> LoadIconCoreAsync(string iconString, string? fontFamily, IRandomAccessStreamReference? streamRef, double scale)
    {
        // 1. Check for Font/Path-based icons (High priority for string checks)
        if (!string.IsNullOrEmpty(iconString))
        {
            // Marshalling back to the UI thread of the specific service instance
            return await _dispatcherQueue
                .EnqueueAsync(() => GetStringIconSource(iconString, fontFamily, scale), LoadingPriorityOnDispatcher)
                .ConfigureAwait(false);
        }

        // 2. Check for Stream-based icons (Bitmap/SVG)
        if (streamRef != null)
        {
            try
            {
                // Perform I/O on the background worker thread (no UI thread blocking)
                using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);

                // Marshalling back to UI thread to create the WinUI Image object
                return await _dispatcherQueue
                    .EnqueueAsync(BuildImageSource, LoadingPriorityOnDispatcher)
                    .ConfigureAwait(false);

                async Task<IconSource?> BuildImageSource()
                {
                    var bitmap = new BitmapImage();
                    if (_iconSize.Width > _iconSize.Height)
                    {
                        bitmap.DecodePixelWidth = (int)(_iconSize.Width * scale);
                    }
                    else
                    {
                        bitmap.DecodePixelHeight = (int)(_iconSize.Height * scale);
                    }

                    await bitmap.SetSourceAsync(bitmapStream);
                    return new ImageIconSource { ImageSource = bitmap };
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used (RELEASE)
            catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {
#if DEBUG
                Logger.LogDebug($"Failed to open icon stream: {ex}");
#endif
                return null;
            }
        }

        return null;
    }

    private IconSource? GetStringIconSource(string iconString, string? fontFamily, double scale)
    {
        // Guaranteed UI thread (called via EnqueueAsync)
        return IconPathConverter.IconSourceMUX(iconString, false, fontFamily, (int)(_iconSize.Width * scale));
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

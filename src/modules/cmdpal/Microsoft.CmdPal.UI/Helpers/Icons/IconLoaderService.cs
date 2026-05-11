// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Channels;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class IconLoaderService : IIconLoaderService
{
    public static readonly Size NoResize = Size.Empty;

    private const DispatcherQueuePriority LoadingPriorityOnDispatcher = DispatcherQueuePriority.Low;
    private const int DefaultIconSize = 256;
    private const int MaxWorkerCount = 4;

    private static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, MaxWorkerCount);

    private readonly Channel<Func<Task>> _highPriorityQueue = Channel.CreateBounded<Func<Task>>(32);
    private readonly Channel<Func<Task>> _lowPriorityQueue = Channel.CreateUnbounded<Func<Task>>();
    private readonly Task[] _workers;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ManagedIconSourceFactory _managedIconSourceFactory;
    private long _nextLoadId;

    public IconLoaderService(DispatcherQueue dispatcherQueue, ManagedIconSourceFactory managedIconSourceFactory)
    {
        _dispatcherQueue = dispatcherQueue;
        _managedIconSourceFactory = managedIconSourceFactory;
        _workers = new Task[WorkerCount];

        for (var i = 0; i < WorkerCount; i++)
        {
            _workers[i] = Task.Run(ProcessQueueAsync);
        }
    }

    public void EnqueueLoad(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        ElementTheme theme,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority = IconLoadPriority.Low)
    {
        var workItem = () => LoadAndCompleteAsync(iconString, fontFamily, streamRef, iconSize, theme, scale, tcs);

        if (priority == IconLoadPriority.High)
        {
            if (_highPriorityQueue.Writer.TryWrite(workItem))
            {
                return;
            }

#if DEBUG
            Logger.LogDebug("High priority icon queue full, falling back to low priority");
#endif
        }

        if (!_lowPriorityQueue.Writer.TryWrite(workItem))
        {
            var exception = new InvalidOperationException("Failed to enqueue icon load because the icon loader queue is unavailable.");
            Logger.LogError("Failed to enqueue icon load", exception);
            tcs.TrySetException(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _highPriorityQueue.Writer.Complete();
        _lowPriorityQueue.Writer.Complete();

        await Task.WhenAll(_workers).ConfigureAwait(false);
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            Func<Task>? workItem;

            if (_highPriorityQueue.Reader.TryRead(out workItem))
            {
                await ExecuteWork(workItem).ConfigureAwait(false);
                continue;
            }

            var highWait = _highPriorityQueue.Reader.WaitToReadAsync().AsTask();
            var lowWait = _lowPriorityQueue.Reader.WaitToReadAsync().AsTask();

            await Task.WhenAny(highWait, lowWait).ConfigureAwait(false);

            // Check if both channels are completed (disposal)
            if (_highPriorityQueue.Reader.Completion.IsCompleted &&
                _lowPriorityQueue.Reader.Completion.IsCompleted)
            {
                // Drain any remaining items
                while (_highPriorityQueue.Reader.TryRead(out workItem))
                {
                    await ExecuteWork(workItem).ConfigureAwait(false);
                }

                while (_lowPriorityQueue.Reader.TryRead(out workItem))
                {
                    await ExecuteWork(workItem).ConfigureAwait(false);
                }

                break;
            }

            if (_highPriorityQueue.Reader.TryRead(out workItem) ||
                _lowPriorityQueue.Reader.TryRead(out workItem))
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
                Logger.LogError("Failed to load icon", ex);
            }
        }
    }

    private async Task LoadAndCompleteAsync(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        ElementTheme theme,
        double scale,
        TaskCompletionSource<IconSource?> tcs)
    {
        try
        {
            var result = await LoadIconCoreAsync(iconString, fontFamily, streamRef, iconSize, theme, scale).ConfigureAwait(false);
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load icon", ex);
            tcs.TrySetException(ex);
        }
    }

    private async Task<IconSource?> LoadIconCoreAsync(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        ElementTheme theme,
        double scale)
    {
        var loadId = Interlocked.Increment(ref _nextLoadId);
        var scaledSize = new Size(iconSize.Width * scale, iconSize.Height * scale);

        if (!string.IsNullOrEmpty(iconString))
        {
            Logger.LogDebug($"[IconLoad:{loadId}] Loading icon string '{TrimForLog(iconString)}' with font family '{fontFamily ?? "(null)"}', target size {DescribeSize(scaledSize)}.");
            return await GetStringIconSourceAsync(iconString, fontFamily, scaledSize, theme).ConfigureAwait(false);
        }

        if (streamRef != null)
        {
            try
            {
                Logger.LogDebug($"[IconLoad:{loadId}] Loading stream-backed icon with stream ref hash 0x{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(streamRef):X8}, target size {DescribeSize(scaledSize)}.");
                using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);
                Logger.LogDebug($"[IconLoad:{loadId}] Opened stream-backed icon stream of {bitmapStream.Size} bytes.");
                return await _managedIconSourceFactory
                    .CreateFromStreamAsync(bitmapStream, sourceUri: null, scaledSize, _dispatcherQueue, LoadingPriorityOnDispatcher)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[IconLoad:{loadId}] Failed to load icon from stream", ex);
                return null;
            }
        }

        Logger.LogDebug($"[IconLoad:{loadId}] No icon string or stream was provided.");
        return null;
    }

    private async Task<IconSource?> GetStringIconSourceAsync(string iconString, string? fontFamily, Size size, ElementTheme theme)
    {
        try
        {
            var managedResult = await _managedIconSourceFactory
                .CreateFromStringAsync(iconString, fontFamily, size, theme, _dispatcherQueue, LoadingPriorityOnDispatcher)
                .ConfigureAwait(false);

            if (managedResult.WasHandled)
            {
                Logger.LogDebug($"Managed icon pipeline handled icon '{TrimForLog(iconString)}'; result source type is '{managedResult.Source?.GetType().Name ?? "(null)"}'.");
                return managedResult.Source;
            }

            Logger.LogWarning($"Managed icon pipeline did not handle icon '{TrimForLog(iconString)}'; returning null icon source.");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create icon source from '{iconString}' with font family '{fontFamily ?? "(null)"}'", ex);
            return null;
        }
    }

    private static string DescribeSize(Size size) => size.IsEmpty ? "Empty" : $"{size.Width:0.##}x{size.Height:0.##}";

    private static string TrimForLog(string value, int maxLength = 200) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}...";
}

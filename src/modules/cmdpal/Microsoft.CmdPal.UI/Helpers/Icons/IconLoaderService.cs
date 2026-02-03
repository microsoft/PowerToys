// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Channels;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.Terminal.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class IconLoaderService : IIconLoaderService
{
    private const DispatcherQueuePriority LoadingPriorityOnDispatcher = DispatcherQueuePriority.Low;
    private const int DefaultIconSize = 256;
    private const int MaxWorkerCount = 4;

    private static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, MaxWorkerCount);

    private readonly Channel<Func<Task>> _highPriorityQueue = Channel.CreateBounded<Func<Task>>(32);
    private readonly Channel<Func<Task>> _lowPriorityQueue = Channel.CreateUnbounded<Func<Task>>();
    private readonly Task[] _workers;
    private readonly DispatcherQueue _dispatcherQueue;

    public IconLoaderService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
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
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority = IconLoadPriority.Low)
    {
        var workItem = () => LoadAndCompleteAsync(iconString, fontFamily, streamRef, iconSize, scale, tcs);

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

        _lowPriorityQueue.Writer.TryWrite(workItem);
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
        double scale,
        TaskCompletionSource<IconSource?> tcs)
    {
        try
        {
            var result = await LoadIconCoreAsync(iconString, fontFamily, streamRef, iconSize, scale).ConfigureAwait(false);
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    private async Task<IconSource?> LoadIconCoreAsync(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale)
    {
        var scaledSize = new Size(iconSize.Width * scale, iconSize.Height * scale);

        if (!string.IsNullOrEmpty(iconString))
        {
            return await _dispatcherQueue
                .EnqueueAsync(() => GetStringIconSource(iconString, fontFamily, scaledSize), LoadingPriorityOnDispatcher)
                .ConfigureAwait(false);
        }

        if (streamRef != null)
        {
            try
            {
                using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);

                return await _dispatcherQueue
                    .EnqueueAsync(BuildImageSource, LoadingPriorityOnDispatcher)
                    .ConfigureAwait(false);

                async Task<IconSource?> BuildImageSource()
                {
                    var bitmap = new BitmapImage();
                    ApplyDecodeSize(bitmap, scaledSize);
                    await bitmap.SetSourceAsync(bitmapStream);
                    return new ImageIconSource { ImageSource = bitmap };
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used
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

    private static void ApplyDecodeSize(BitmapImage bitmap, Size size)
    {
        if (size.IsEmpty)
        {
            return;
        }

        if (size.Width >= size.Height)
        {
            bitmap.DecodePixelWidth = (int)size.Width;
        }
        else
        {
            bitmap.DecodePixelHeight = (int)size.Height;
        }
    }

    private static IconSource? GetStringIconSource(string iconString, string? fontFamily, Size size)
    {
        var iconSize = (int)Math.Max(size.Width, size.Height);
        if (iconSize == 0)
        {
            iconSize = DefaultIconSize;
        }

        return IconPathConverter.IconSourceMUX(iconString, false, fontFamily, iconSize);
    }
}

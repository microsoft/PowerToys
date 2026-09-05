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
    public static readonly Size NoResize = Size.Empty;

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

    public bool TryEnqueueLoad(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority = IconLoadPriority.Low,
        IconLoadMeasurement? diagnostics = null)
    {
        if (priority == IconLoadPriority.High)
        {
            var highPriorityWorkItem = () => LoadAndCompleteAsync(iconString, fontFamily, streamRef, iconSize, scale, tcs, diagnostics);
            if (_highPriorityQueue.Writer.TryWrite(highPriorityWorkItem))
            {
                RecordEnqueued(diagnostics, IconLoadPriority.High);
                return true;
            }

#if DEBUG
            Logger.LogDebug("High priority icon queue full, falling back to low priority");
#endif
        }

        var lowPriorityWorkItem = () => LoadAndCompleteAsync(iconString, fontFamily, streamRef, iconSize, scale, tcs, diagnostics);
        if (_lowPriorityQueue.Writer.TryWrite(lowPriorityWorkItem))
        {
            RecordEnqueued(diagnostics, IconLoadPriority.Low);
            return true;
        }

        diagnostics?.Rejected();
        return false;

        static void RecordEnqueued(IconLoadMeasurement? diagnostics, IconLoadPriority actualPriority)
        {
            try
            {
                diagnostics?.Enqueued(actualPriority);
            }
            catch (Exception ex)
            {
                // Diagnostics must not fail work that was already published to the loader queue.
                Logger.LogError("Failed to record icon load enqueue diagnostics", ex);
            }
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
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadMeasurement? diagnostics)
    {
        try
        {
            if (diagnostics is not null && !await diagnostics.WorkerStartingAsync(WorkerCount).ConfigureAwait(false))
            {
                diagnostics = null;
            }

            var result = await LoadIconCoreAsync(iconString, fontFamily, streamRef, iconSize, scale, diagnostics).ConfigureAwait(false);
            diagnostics?.Complete();
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            diagnostics?.Fail();
            tcs.TrySetException(ex);
        }
    }

    private async Task<IconSource?> LoadIconCoreAsync(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale,
        IconLoadMeasurement? diagnostics)
    {
        var scaledSize = iconSize.IsEmpty
            ? iconSize
            : new Size(iconSize.Width * scale, iconSize.Height * scale);

        if (!string.IsNullOrEmpty(iconString))
        {
            var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait() ?? 0;
            return await _dispatcherQueue
                .EnqueueAsync(
                    () =>
                    {
                        var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
                        try
                        {
                            var result = GetStringIconSource(iconString, fontFamily, scaledSize);
                            diagnostics?.SetResult(result);
                            return result;
                        }
                        finally
                        {
                            diagnostics?.DispatcherCompleted(dispatcherStartedAt);
                        }
                    },
                    LoadingPriorityOnDispatcher)
                .ConfigureAwait(false);
        }

        if (streamRef != null)
        {
            try
            {
                var preparationStartedAt = diagnostics?.BeginBackgroundPreparation() ?? 0;
                using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);
                diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);

                var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait() ?? 0;
                return await _dispatcherQueue
                    .EnqueueAsync(BuildImageSource, LoadingPriorityOnDispatcher)
                    .ConfigureAwait(false);

                async Task<IconSource?> BuildImageSource()
                {
                    var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
                    try
                    {
                        var bitmap = new BitmapImage();
                        ApplyDecodeSize(bitmap, scaledSize);
                        await bitmap.SetSourceAsync(bitmapStream);
                        var result = new ImageIconSource { ImageSource = bitmap };
                        diagnostics?.SetResult(result);
                        return result;
                    }
                    finally
                    {
                        diagnostics?.DispatcherCompleted(dispatcherStartedAt);
                    }
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
        var iconSize = size.IsEmpty
            ? DefaultIconSize
            : (int)Math.Max(size.Width, size.Height);
        return IconPathConverter.IconSourceMUX(iconString, fontFamily, iconSize);
    }
}

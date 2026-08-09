// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

    private readonly IconLoadQueue _queue = new(WorkerCount);
    private readonly Task[] _workers;
    private readonly DispatcherQueue _dispatcherQueue;
    private FontFamily? _fluentIconFontFamily;
    private FontFamily? _emojiFontFamily;
    private FontFamily? _generalFontFamily;
    private int _directGlyphFailureLogged;

    public IconLoaderService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _workers = new Task[WorkerCount];

        for (var i = 0; i < WorkerCount; i++)
        {
            _workers[i] = Task.Run(ProcessQueueAsync);
        }

        _ = _queue.Completion.ContinueWith(
            static task => Logger.LogError("Icon load scheduler failed", task.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public bool TryLoadGlyph(
        string? iconString,
        string? fontFamily,
        Size iconSize,
        double scale,
        [MaybeNullWhen(false)] out IconSource result)
    {
        result = null;

        // IconSource is a XAML object. If a caller ever reaches the provider away from
        // the UI thread, preserve the existing dispatcher-based path.
        if (!_dispatcherQueue.HasThreadAccess || string.IsNullOrEmpty(iconString))
        {
            return false;
        }

        try
        {
            var glyphKind = FontIconGlyphClassifier.Classify(iconString);
            if (glyphKind is FontIconGlyphKind.Invalid or FontIconGlyphKind.None)
            {
                return false;
            }

            result = new FontIconSource
            {
                FontFamily = GetOrCreateFontFamily(glyphKind, fontFamily),
                FontSize = FontIconSizeCalculator.Calculate(iconSize, scale, DefaultIconSize),
                Glyph = iconString,
            };
            return true;
        }
        catch (Exception ex)
        {
            // The general converter has its own fallback behavior. Let it handle any
            // input that cannot be represented by this narrow glyph fast path.
            if (Interlocked.Exchange(ref _directGlyphFailureLogged, 1) == 0)
            {
                Logger.LogError("Direct glyph construction failed; falling back to queued icon loading", ex);
            }

            result = null;
            return false;
        }
    }

    private FontFamily GetOrCreateFontFamily(FontIconGlyphKind glyphKind, string? requestedFontFamily)
    {
        var familySource = FontIconGlyphClassifier.GetFontFamily(glyphKind, requestedFontFamily);
        if (!string.IsNullOrEmpty(requestedFontFamily))
        {
            return new FontFamily(familySource);
        }

        // TryLoadGlyph gates this method to the service's dispatcher thread, so these
        // XAML objects can be reused without a lock or cross-STA sharing.
        return glyphKind switch
        {
            FontIconGlyphKind.FluentSymbol =>
                _fluentIconFontFamily ??= new FontFamily(familySource),
            FontIconGlyphKind.Emoji =>
                _emojiFontFamily ??= new FontFamily(familySource),
            _ => _generalFontFamily ??= new FontFamily(familySource),
        };
    }

    public bool TryEnqueueLoad(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority = IconLoadPriority.Low,
        IconLoadMeasurement? diagnostics = null,
        IconLoadDemand? demand = null)
    {
        demand ??= IconLoadDemand.CreateDemanded();
        var operation = new IconLoadOperation(
            this,
            iconString,
            fontFamily,
            streamRef,
            iconSize,
            scale,
            tcs,
            diagnostics);
        if (_queue.TryEnqueue(operation, priority, demand, out var actualPriority))
        {
#if DEBUG
            if (priority == IconLoadPriority.High && actualPriority == IconLoadPriority.Low)
            {
                Logger.LogDebug("High priority icon queue full, falling back to low priority");
            }
#endif
            return true;
        }

        diagnostics?.Rejected();
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Complete();
        var tasks = new Task[_workers.Length + 1];
        _workers.CopyTo(tasks, 0);
        tasks[^1] = _queue.Completion;
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ProcessQueueAsync()
    {
        while (await _queue.DequeueAsync().ConfigureAwait(false) is { } operation)
        {
            try
            {
                await operation.ExecuteAsync().ConfigureAwait(false);
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
            var preparationStartedAt = diagnostics?.BeginBackgroundPreparation() ?? 0;
            var targetSize = scaledSize.IsEmpty
                ? DefaultIconSize
                : (int)Math.Max(scaledSize.Width, scaledSize.Height);
            var preparedIcon = IconPathConverter.Prepare(iconString, fontFamily, targetSize);
            diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);

            try
            {
                var materializationKind = diagnostics is null
                    ? IconDispatcherMaterializationKind.Unknown
                    : GetDispatcherMaterializationKind(preparedIcon);
                var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait(materializationKind) ?? 0;
                try
                {
                    return await _dispatcherQueue
                        .EnqueueAsync(CreateIconSourceOnDispatcher, LoadingPriorityOnDispatcher)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // This is a no-op after the callback has started or completed.
                    diagnostics?.DispatcherWaitFailed(dispatcherEnqueuedAt);
                    throw;
                }

                async Task<IconSource?> CreateIconSourceOnDispatcher()
                {
                    var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
                    var suspensionStartedAt = 0L;
                    var continuationStartedAt = 0L;
                    try
                    {
                        var operation = IconPathConverter.CreateIconSourceAsync(preparedIcon);
                        if (operation.IsCompleted)
                        {
                            var synchronousResult = await operation;
                            diagnostics?.SetResult(synchronousResult);
                            return synchronousResult;
                        }

                        suspensionStartedAt = diagnostics?.DispatcherUiSliceCompleted(
                            dispatcherStartedAt,
                            IconDispatcherUiSliceKind.BeforeAsyncSuspension) ?? 0;
                        IconSource result;
                        try
                        {
                            result = await operation;
                        }
                        finally
                        {
                            if (suspensionStartedAt != 0)
                            {
                                continuationStartedAt = diagnostics?.DispatcherAsyncSuspensionCompleted(
                                    suspensionStartedAt) ?? 0;
                            }
                        }

                        diagnostics?.SetResult(result);
                        return result;
                    }
                    finally
                    {
                        if (suspensionStartedAt == 0)
                        {
                            diagnostics?.DispatcherUiSliceCompleted(
                                dispatcherStartedAt,
                                IconDispatcherUiSliceKind.SynchronousCallback);
                        }
                        else if (continuationStartedAt != 0)
                        {
                            diagnostics?.DispatcherUiSliceCompleted(
                                continuationStartedAt,
                                IconDispatcherUiSliceKind.AsyncContinuation);
                        }

                        diagnostics?.DispatcherCompleted(dispatcherStartedAt);
                    }
                }
            }
            finally
            {
                preparedIcon.Dispose();
            }
        }

        if (streamRef != null)
        {
            try
            {
                var preparationStartedAt = diagnostics?.BeginBackgroundPreparation() ?? 0;
                using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);
                diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);

                var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait(
                    IconDispatcherMaterializationKind.BitmapStream) ?? 0;
                try
                {
                    return await _dispatcherQueue
                        .EnqueueAsync(BuildImageSource, LoadingPriorityOnDispatcher)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // This is a no-op after the callback has started or completed.
                    diagnostics?.DispatcherWaitFailed(dispatcherEnqueuedAt);
                    throw;
                }

                async Task<IconSource?> BuildImageSource()
                {
                    var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
                    var suspensionStartedAt = 0L;
                    var continuationStartedAt = 0L;
                    try
                    {
                        var bitmap = new BitmapImage();
                        ApplyDecodeSize(bitmap, scaledSize);
                        var operation = bitmap.SetSourceAsync(bitmapStream);
                        suspensionStartedAt = diagnostics?.DispatcherUiSliceCompleted(
                            dispatcherStartedAt,
                            IconDispatcherUiSliceKind.BeforeAsyncSuspension) ?? 0;
                        try
                        {
                            await operation;
                        }
                        finally
                        {
                            if (suspensionStartedAt != 0)
                            {
                                continuationStartedAt = diagnostics?.DispatcherAsyncSuspensionCompleted(
                                    suspensionStartedAt) ?? 0;
                            }
                        }

                        var result = new ImageIconSource { ImageSource = bitmap };
                        diagnostics?.SetResult(result);
                        return result;
                    }
                    finally
                    {
                        if (suspensionStartedAt == 0)
                        {
                            diagnostics?.DispatcherUiSliceCompleted(
                                dispatcherStartedAt,
                                IconDispatcherUiSliceKind.SynchronousCallback);
                        }
                        else if (continuationStartedAt != 0)
                        {
                            diagnostics?.DispatcherUiSliceCompleted(
                                continuationStartedAt,
                                IconDispatcherUiSliceKind.AsyncContinuation);
                        }

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

    private static IconDispatcherMaterializationKind GetDispatcherMaterializationKind(
        IconPathConverter.PreparedIcon preparedIcon) =>
        preparedIcon.Kind switch
        {
            IconPathConverter.PreparedIconKind.Empty => IconDispatcherMaterializationKind.Empty,
            IconPathConverter.PreparedIconKind.BitmapUri => IconDispatcherMaterializationKind.BitmapUri,
            IconPathConverter.PreparedIconKind.SvgUri => IconDispatcherMaterializationKind.SvgUri,
            IconPathConverter.PreparedIconKind.Glyph => IconDispatcherMaterializationKind.Glyph,
            IconPathConverter.PreparedIconKind.Binary => IconDispatcherMaterializationKind.Binary,
            _ => IconDispatcherMaterializationKind.Unknown,
        };

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

    private sealed class IconLoadOperation : IconLoadQueue.Operation
    {
        private readonly IconLoaderService _owner;
        private readonly string? _iconString;
        private readonly string? _fontFamily;
        private readonly IRandomAccessStreamReference? _streamRef;
        private readonly Size _iconSize;
        private readonly double _scale;
        private readonly TaskCompletionSource<IconSource?> _completion;
        private readonly IconLoadMeasurement? _diagnostics;

        public IconLoadOperation(
            IconLoaderService owner,
            string? iconString,
            string? fontFamily,
            IRandomAccessStreamReference? streamRef,
            Size iconSize,
            double scale,
            TaskCompletionSource<IconSource?> completion,
            IconLoadMeasurement? diagnostics)
        {
            _owner = owner;
            _iconString = iconString;
            _fontFamily = fontFamily;
            _streamRef = streamRef;
            _iconSize = iconSize;
            _scale = scale;
            _completion = completion;
            _diagnostics = diagnostics;
        }

        public override void Enqueued(IconLoadPriority priority, int workerCount)
        {
            try
            {
                _diagnostics?.Enqueued(priority, workerCount);
            }
            catch (Exception ex)
            {
                // Diagnostics must not fail work that the loader is about to publish.
                Logger.LogError("Failed to record icon load enqueue diagnostics", ex);
            }
        }

        public override Task ExecuteAsync() =>
            _owner.LoadAndCompleteAsync(
                _iconString,
                _fontFamily,
                _streamRef,
                _iconSize,
                _scale,
                _completion,
                _diagnostics);

        public override void Fail(Exception failure)
        {
            try
            {
                _diagnostics?.Fail();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to record abandoned icon load diagnostics", ex);
            }

            _completion.TrySetException(failure);
        }
    }
}

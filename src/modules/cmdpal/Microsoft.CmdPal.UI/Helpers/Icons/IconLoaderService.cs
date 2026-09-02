// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
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
    private readonly IShellItemIconExtractor _shellItemIconExtractor;
    private readonly ShellIconLocationResolver _shellItemIconLocationResolver;
    private FontFamily? _fluentIconFontFamily;
    private FontFamily? _emojiFontFamily;
    private FontFamily? _generalFontFamily;
    private int _directGlyphFailureLogged;

    public ShellIconLocationCache ShellIconLocations { get; }

    public IconLoaderService(DispatcherQueue dispatcherQueue)
        : this(
            dispatcherQueue,
            ShellItemIconLocator.Instance,
            ShellItemIconExtractor.Instance,
            new ShellIconLocationCache())
    {
    }

    internal IconLoaderService(
        DispatcherQueue dispatcherQueue,
        IShellItemIconLocator shellItemIconLocator,
        IShellItemIconExtractor shellItemIconExtractor,
        ShellIconLocationCache shellIconLocations)
    {
        _dispatcherQueue = dispatcherQueue;
        _shellItemIconExtractor = shellItemIconExtractor;
        ShellIconLocations = shellIconLocations;
        _shellItemIconLocationResolver = new ShellIconLocationResolver(
            shellItemIconLocator,
            shellIconLocations);
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
        ElementTheme theme,
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
            theme,
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

    public bool TryEnqueueShellItemLoad(
        ShellItemIconRequest request,
        LocatedShellIcon? locatedIcon,
        Size iconSize,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority = IconLoadPriority.Low,
        IconLoadMeasurement? diagnostics = null,
        IconLoadDemand? demand = null,
        IShellItemIconLoadCoordinator? coordinator = null,
        ShellIconMeasurement shellDiagnostics = default)
    {
        demand ??= IconLoadDemand.CreateDemanded();
        var operation = new ShellItemIconLoadOperation(
            this,
            request,
            locatedIcon,
            iconSize,
            scale,
            tcs,
            diagnostics,
            coordinator,
            shellDiagnostics);
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
        ElementTheme theme,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadMeasurement? diagnostics)
    {
        var workerDiagnostics = diagnostics;
        try
        {
            if (diagnostics is not null && !await diagnostics.WorkerStartingAsync(WorkerCount).ConfigureAwait(false))
            {
                diagnostics = null;
            }

            var result = await LoadIconCoreAsync(iconString, fontFamily, streamRef, iconSize, scale, theme, diagnostics).ConfigureAwait(false);
            diagnostics?.Complete();
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            diagnostics?.Fail();
            tcs.TrySetException(ex);
        }
        finally
        {
            workerDiagnostics?.WorkerReleased();
        }
    }

    private async Task LoadShellItemAndCompleteAsync(
        ShellItemIconRequest request,
        LocatedShellIcon? knownLocation,
        Size iconSize,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadMeasurement? diagnostics,
        IShellItemIconLoadCoordinator? coordinator,
        ShellIconMeasurement shellDiagnostics)
    {
        var workerDiagnostics = diagnostics;
        try
        {
            if (diagnostics is not null && !await diagnostics.WorkerStartingAsync(WorkerCount).ConfigureAwait(false))
            {
                diagnostics = null;
            }

            var preparationStartedAt = diagnostics?.BeginBackgroundPreparation() ?? 0;
            var locatedIcon = _shellItemIconLocationResolver.GetCurrentOrCached(
                request,
                knownLocation);
            if (locatedIcon is null)
            {
                var identityStartedAt = shellDiagnostics.BeginIdentityResolution();
                locatedIcon = _shellItemIconLocationResolver.Resolve(request);
                shellDiagnostics.IdentityResolved(locatedIcon.Value.Identity.Kind, identityStartedAt);
            }

            if (coordinator?.TryJoinExistingLoad(locatedIcon.Value, out var sharedTask) == true)
            {
                diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);
                ForwardSharedLoad(sharedTask, tcs, diagnostics);
                return;
            }

            var scaledSize = iconSize.IsEmpty
                ? iconSize
                : new Size(iconSize.Width * scale, iconSize.Height * scale);
            var targetPixelSize = scaledSize.IsEmpty
                ? DefaultIconSize
                : (int)Math.Max(scaledSize.Width, scaledSize.Height);
            var extractionStartedAt = shellDiagnostics.BeginExtraction();
            ShellIconExtractionResult extractionResult;
            try
            {
                extractionResult = await _shellItemIconExtractor
                    .ExtractAsync(locatedIcon.Value, targetPixelSize)
                    .ConfigureAwait(false);
                shellDiagnostics.ExtractionCompleted(
                    extractionStartedAt,
                    locatedIcon.Value.Identity.Kind,
                    extractionResult.HasContent);
            }
            catch
            {
                shellDiagnostics.ExtractionFailed(
                    extractionStartedAt,
                    locatedIcon.Value.Identity.Kind);
                throw;
            }

            using (extractionResult)
            {
                if (extractionResult.ImageListSize is { } imageListSize)
                {
                    shellDiagnostics.SystemImageListExtracted(
                        imageListSize,
                        extractionResult.RequestedPixelSize,
                        extractionResult.SourceWidth,
                        extractionResult.SourceHeight,
                        extractionResult.HIconConversionTicks);
                }

                diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);
                IconSource? result;
                if (extractionResult.TakeSoftwareBitmap() is { } softwareBitmap)
                {
                    result = await CreateSoftwareBitmapIconSourceAsync(softwareBitmap, diagnostics).ConfigureAwait(false);
                }
                else if (extractionResult.BitmapStream is { } bitmapStream)
                {
                    result = await CreateImageIconSourceAsync(bitmapStream, scaledSize, diagnostics).ConfigureAwait(false);
                }
                else
                {
                    result = await GetShellItemFallbackSourceAsync(diagnostics).ConfigureAwait(false);
                }

                diagnostics?.Complete();
                tcs.TrySetResult(result);
            }
        }
        catch (Exception ex)
        {
            diagnostics?.Fail();
            tcs.TrySetException(ex);
        }
        finally
        {
            workerDiagnostics?.WorkerReleased();
        }
    }

    private async Task<IconSource?> GetShellItemFallbackSourceAsync(IconLoadMeasurement? diagnostics)
    {
        var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait(
            IconDispatcherMaterializationKind.SvgUri) ?? 0;
        try
        {
            return await _dispatcherQueue
                .EnqueueAsync(CreateFallbackSource, LoadingPriorityOnDispatcher)
                .ConfigureAwait(false);
        }
        catch
        {
            diagnostics?.DispatcherWaitFailed(dispatcherEnqueuedAt);
            throw;
        }

        IconSource? CreateFallbackSource()
        {
            var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
            try
            {
                var result = ShellItemIconFallback.GetOrCreate();
                diagnostics?.SetResult(result);
                return result;
            }
            finally
            {
                diagnostics?.DispatcherUiSliceCompleted(
                    dispatcherStartedAt,
                    IconDispatcherUiSliceKind.SynchronousCallback);
                diagnostics?.DispatcherCompleted(dispatcherStartedAt);
            }
        }
    }

    private static void ForwardSharedLoad(
        Task<IconSource?> sharedTask,
        TaskCompletionSource<IconSource?> completion,
        IconLoadMeasurement? diagnostics)
    {
        _ = sharedTask.ContinueWith(
            completed =>
            {
                try
                {
                    if (completed.IsCompletedSuccessfully)
                    {
                        diagnostics?.SetResult(completed.Result);
                        diagnostics?.Complete();
                    }
                    else
                    {
                        diagnostics?.Fail();
                    }
                }
                catch (Exception ex)
                {
                    // Diagnostic bookkeeping must not change the shared load's outcome.
                    Logger.LogError("Failed to record forwarded Shell icon diagnostics", ex);
                }

                if (completed.IsCompletedSuccessfully)
                {
                    completion.TrySetResult(completed.Result);
                }
                else if (completed.IsCanceled)
                {
                    completion.TrySetCanceled();
                }
                else
                {
                    completion.TrySetException(completed.Exception!.InnerExceptions);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<IconSource?> LoadIconCoreAsync(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale,
        ElementTheme theme,
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
            IconProtocolProcessingResult? protocolResult = null;
            IconPathConverter.PreparedIcon? preparedIcon = null;

            try
            {
                if (IconProtocolRegistry.Find(iconString) is not { } protocolProcessor)
                {
                    preparedIcon = IconPathConverter.Prepare(iconString, fontFamily, targetSize, theme);
                }
                else if (!protocolProcessor.TryPrepareSynchronously(iconString, targetSize, theme, out preparedIcon))
                {
                    protocolResult = await protocolProcessor.PrepareAsync(iconString, targetSize, theme).ConfigureAwait(false);
                    if (protocolResult.BitmapStream is { } bitmapStream)
                    {
                        diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);
                        return await CreateImageIconSourceAsync(bitmapStream, scaledSize, diagnostics).ConfigureAwait(false);
                    }

                    preparedIcon = protocolResult.TakePreparedIcon();
                    if (preparedIcon is null && protocolResult.FallbackIconString is { } fallbackIconString)
                    {
                        preparedIcon = IconPathConverter.Prepare(fallbackIconString, fontFamily, targetSize, theme);
                    }
                }

                preparedIcon ??= IconPathConverter.PreparedIcon.Empty();
                diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);

                var materializationKind = diagnostics is null
                    ? IconDispatcherMaterializationKind.Unknown
                    : GetDispatcherMaterializationKind(preparedIcon);
                var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait(materializationKind) ?? 0;

                // Keep the dispatcher callback synchronous for glyph and URI sources.
                // The returned ValueTask carries only binary transfer work beyond it.
                try
                {
                    var materialization = await _dispatcherQueue
                        .EnqueueAsync(CreateIconSourceOnDispatcher, LoadingPriorityOnDispatcher)
                        .ConfigureAwait(false);
                    return await materialization.ConfigureAwait(false);
                }
                catch
                {
                    // This is a no-op after the callback has started or completed.
                    diagnostics?.DispatcherWaitFailed(dispatcherEnqueuedAt);
                    throw;
                }

                ValueTask<IconSource?> CreateIconSourceOnDispatcher()
                {
                    var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
                    var completionOwnedByCallback = true;
                    try
                    {
                        if (IconPathConverter.TryCreateIconSourceSynchronously(preparedIcon, out var result))
                        {
                            diagnostics?.SetResult(result);
                            return ValueTask.FromResult<IconSource?>(result);
                        }

                        var materializationInner = CompleteAsynchronousMaterializationAsync(dispatcherStartedAt);

                        // The asynchronous continuation now owns the single timing-completion notification.
                        completionOwnedByCallback = false;
                        return materializationInner;
                    }
                    finally
                    {
                        if (completionOwnedByCallback)
                        {
                            diagnostics?.DispatcherUiSliceCompleted(
                                dispatcherStartedAt,
                                IconDispatcherUiSliceKind.SynchronousCallback);
                            diagnostics?.DispatcherCompleted(dispatcherStartedAt);
                        }
                    }
                }

                async ValueTask<IconSource?> CompleteAsynchronousMaterializationAsync(long dispatcherStartedAt)
                {
                    var suspensionStartedAt = 0L;
                    var continuationStartedAt = 0L;
                    try
                    {
                        var operation = IconPathConverter.CompleteIconSourceCreationAsync(preparedIcon);
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
                preparedIcon?.Dispose();
                protocolResult?.Dispose();
            }
        }

        if (streamRef != null)
        {
            try
            {
                var preparationStartedAt = diagnostics?.BeginBackgroundPreparation() ?? 0;
                using var bitmapStream = await streamRef.OpenReadAsync().AsTask().ConfigureAwait(false);
                diagnostics?.CompleteBackgroundPreparation(preparationStartedAt);
                return await CreateImageIconSourceAsync(bitmapStream, scaledSize, diagnostics).ConfigureAwait(false);
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
            IconPathConverter.PreparedIconKind.SvgData => IconDispatcherMaterializationKind.SvgData,
            _ => IconDispatcherMaterializationKind.Unknown,
        };

    private async Task<IconSource?> CreateSoftwareBitmapIconSourceAsync(
        SoftwareBitmap softwareBitmap,
        IconLoadMeasurement? diagnostics)
    {
        var ownershipTransferredToMaterializer = 0;
        var dispatcherEnqueuedAt = diagnostics?.BeginDispatcherWait(
            IconDispatcherMaterializationKind.Binary) ?? 0;
        try
        {
            var materialization = await _dispatcherQueue
                .EnqueueAsync(CreateIconSourceOnDispatcher, LoadingPriorityOnDispatcher)
                .ConfigureAwait(false);
            return await materialization.ConfigureAwait(false);
        }
        catch
        {
            // This is a no-op after the callback has started or completed.
            diagnostics?.DispatcherWaitFailed(dispatcherEnqueuedAt);
            throw;
        }
        finally
        {
            if (Volatile.Read(ref ownershipTransferredToMaterializer) == 0)
            {
                softwareBitmap.Dispose();
            }
        }

        ValueTask<IconSource?> CreateIconSourceOnDispatcher()
        {
            var dispatcherStartedAt = diagnostics?.DispatcherStarted(dispatcherEnqueuedAt) ?? 0;
            return CompleteMaterializationAsync(dispatcherStartedAt);
        }

        async ValueTask<IconSource?> CompleteMaterializationAsync(long dispatcherStartedAt)
        {
            var suspensionStartedAt = 0L;
            var continuationStartedAt = 0L;
            try
            {
                // The converter now owns the bitmap and either disposes it on failure
                // or transfers its lifetime to XAML on success.
                Interlocked.Exchange(ref ownershipTransferredToMaterializer, 1);
                var operation = IconPathConverter.CreateBinaryIconSourceAsync(softwareBitmap);
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

    private async Task<IconSource?> CreateImageIconSourceAsync(
        IRandomAccessStream bitmapStream,
        Size scaledSize,
        IconLoadMeasurement? diagnostics)
    {
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
        private readonly ElementTheme _theme;
        private readonly TaskCompletionSource<IconSource?> _completion;
        private readonly IconLoadMeasurement? _diagnostics;

        public IconLoadOperation(
            IconLoaderService owner,
            string? iconString,
            string? fontFamily,
            IRandomAccessStreamReference? streamRef,
            Size iconSize,
            double scale,
            ElementTheme theme,
            TaskCompletionSource<IconSource?> completion,
            IconLoadMeasurement? diagnostics)
        {
            _owner = owner;
            _iconString = iconString;
            _fontFamily = fontFamily;
            _streamRef = streamRef;
            _iconSize = iconSize;
            _scale = scale;
            _theme = theme;
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
                _theme,
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

    private sealed class ShellItemIconLoadOperation : IconLoadQueue.Operation
    {
        private readonly IconLoaderService _owner;
        private readonly ShellItemIconRequest _request;
        private readonly LocatedShellIcon? _locatedIcon;
        private readonly Size _iconSize;
        private readonly double _scale;
        private readonly TaskCompletionSource<IconSource?> _completion;
        private readonly IconLoadMeasurement? _diagnostics;
        private readonly IShellItemIconLoadCoordinator? _coordinator;
        private readonly ShellIconMeasurement _shellDiagnostics;

        public ShellItemIconLoadOperation(
            IconLoaderService owner,
            ShellItemIconRequest request,
            LocatedShellIcon? locatedIcon,
            Size iconSize,
            double scale,
            TaskCompletionSource<IconSource?> completion,
            IconLoadMeasurement? diagnostics,
            IShellItemIconLoadCoordinator? coordinator,
            ShellIconMeasurement shellDiagnostics)
        {
            _owner = owner;
            _request = request;
            _locatedIcon = locatedIcon;
            _iconSize = iconSize;
            _scale = scale;
            _completion = completion;
            _diagnostics = diagnostics;
            _coordinator = coordinator;
            _shellDiagnostics = shellDiagnostics;
        }

        public override void Enqueued(IconLoadPriority priority, int workerCount)
        {
            try
            {
                _diagnostics?.Enqueued(priority, workerCount);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to record icon load enqueue diagnostics", ex);
            }
        }

        public override Task ExecuteAsync() =>
            _owner.LoadShellItemAndCompleteAsync(
                _request,
                _locatedIcon,
                _iconSize,
                _scale,
                _completion,
                _diagnostics,
                _coordinator,
                _shellDiagnostics);

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

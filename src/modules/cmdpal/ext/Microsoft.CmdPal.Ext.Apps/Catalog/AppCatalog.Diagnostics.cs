// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.Logging;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

public sealed partial class AppCatalog
{
    private bool ShouldLogDiagnostics()
    {
        return _diagnosticsEnabled() && _logger.IsEnabled(LogLevel.Information);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to initialize application source '{SourceId}'.")]
    private static partial void LogSourceInitializationFailed(MEL.ILogger logger, string sourceId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to refresh application source '{SourceId}'.")]
    private static partial void LogSourceRefreshFailed(MEL.ILogger logger, string sourceId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to save the application catalog cache.")]
    private static partial void LogCacheSaveFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Application catalog refresh failed.")]
    private static partial void LogCatalogRefreshFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Application catalog refresh-state notification failed.")]
    private static partial void LogRefreshStateNotificationFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Application catalog refresh recovery failed.")]
    private static partial void LogRefreshRecoveryFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Application source provider returned duplicate ID '{SourceId}'.")]
    private static partial void LogDuplicateSourceId(MEL.ILogger logger, string sourceId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Application catalog policy reprojection failed.")]
    private static partial void LogCatalogReprojectionFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Application catalog change notification failed.")]
    private static partial void LogCatalogChangeNotificationFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Startup {Phase}: {DurationMs} ms; sources={SourceCount}, cached={CachedSourceCount}, visible={VisibleCount}.")]
    private static partial void LogDiagnosticStartup(MEL.ILogger logger, string phase, double durationMs, int sourceCount, int cachedSourceCount, int visibleCount);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Batch {BatchId} source {SourceId} started: background={Background}, requestedIncremental={RequestedIncremental}, dirtyPaths={DirtyPathCount}, queued={QueuedMs} ms.")]
    private static partial void LogDiagnosticSourceStarted(MEL.ILogger logger, long batchId, string sourceId, bool background, bool requestedIncremental, int dirtyPathCount, double? queuedMs);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Batch {BatchId} source {SourceId} loaded in {DurationMs} ms: incremental={Incremental}, items={ItemCount}, complete={Complete}, failedPaths={FailedPathCount}, retryPaths={RetryPathCount}.")]
    private static partial void LogDiagnosticSourceCompleted(MEL.ILogger logger, long batchId, string sourceId, double durationMs, bool incremental, int itemCount, bool complete, int? failedPathCount, int retryPathCount);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Batch {BatchId} source {SourceId} {Outcome} after {DurationMs} ms.")]
    private static partial void LogDiagnosticSourceStopped(MEL.ILogger logger, long batchId, string sourceId, string outcome, double durationMs);

    [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Batch {BatchId} source {SourceId} published {ChangeCount} changes: publication={PublicationMs} ms, build={BuildMs} ms, observers={ObserverMs} ms, visible={VisibleCount}, hidden={HiddenCount}.")]
    private static partial void LogDiagnosticPublication(MEL.ILogger logger, long batchId, string sourceId, int changeCount, double publicationMs, double? buildMs, double observerMs, int visibleCount, int hiddenCount);

    [LoggerMessage(EventId = 17, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Batch {BatchId} source {SourceId} waited {DelayMs} ms from scan completion to snapshot application.")]
    private static partial void LogDiagnosticPublicationDelay(MEL.ILogger logger, long batchId, string sourceId, double delayMs);

    [LoggerMessage(EventId = 18, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Batch {BatchId} finished in {DurationMs} ms: cacheAttempted={CacheAttempted}, cache={CacheMs} ms.")]
    private static partial void LogDiagnosticBatchCompleted(MEL.ILogger logger, long batchId, double durationMs, bool cacheAttempted, double cacheMs);

    [LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Source {SourceId} initialization {Phase}: {DurationMs} ms.")]
    private static partial void LogDiagnosticSourceInitialization(MEL.ILogger logger, string sourceId, string phase, double durationMs);

    [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Cached snapshot built in {BuildMs} ms: visible={VisibleCount}.")]
    private static partial void LogDiagnosticCachePublication(MEL.ILogger logger, double? buildMs, int visibleCount);
}

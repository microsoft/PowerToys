// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PowerToys.WorkspacesMCP.Services;

/// <summary>
/// Phase 1: periodic full refresh workspace monitor (every 5s).
/// Later phases can add WinEvent hooks + incremental updates.
/// </summary>
public class WorkspaceMonitor : BackgroundService
{
    private readonly WorkspaceStateProvider _provider;
    private readonly ILogger<WorkspaceMonitor> _logger;
    private readonly WindowsApiService _windowsApi;
    private long _version;

    private static class Log
    {
        private static readonly Action<ILogger, Exception?> _monitorStarted =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(1000, nameof(MonitorStarted)),
                "WorkspaceMonitor started");

        private static readonly Action<ILogger, Exception?> _snapshotFailed =
            LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(1001, nameof(SnapshotRefreshFailed)),
                "Workspace snapshot refresh failed");

        public static void MonitorStarted(ILogger logger) => _monitorStarted(logger, null);

        public static void SnapshotRefreshFailed(ILogger logger, Exception ex) => _snapshotFailed(logger, ex);
    }

    public WorkspaceMonitor(WorkspaceStateProvider provider, ILogger<WorkspaceMonitor> logger)
    {
        _provider = provider;
        _logger = logger;
        _windowsApi = new WindowsApiService();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.MonitorStarted(_logger);
        await RefreshAsync(stoppingToken); // eager first snapshot

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var apps = await _windowsApi.GetRunningApplicationsAsync();
            var windows = await _windowsApi.GetAllWindowsAsync();
            var snapshot = new ImmutableWorkspaceSnapshot(
                TimestampUtc: DateTime.UtcNow,
                Apps: apps,
                Windows: windows,
                VisibleWindows: windows.Count(w => w.IsVisible),
                Version: Interlocked.Increment(ref _version));

            _provider.Publish(snapshot);
        }
        catch (Exception ex)
        {
            Log.SnapshotRefreshFailed(_logger, ex);
        }
    }
}

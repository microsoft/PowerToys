// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.Extensions.DependencyInjection;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class InternalPageViewModel : ObservableObject
{
    private static readonly System.Text.CompositeFormat GarbageCollectionSummaryFormat = System.Text.CompositeFormat.Parse(GetString("InternalPage_IconCacheDiagnostics_GarbageCollectionSummaryFormat"));
    private static readonly System.Text.CompositeFormat CachedProviderSummaryFormat = System.Text.CompositeFormat.Parse(GetString("InternalPage_IconCacheDiagnostics_SummaryFormat"));
    private static readonly System.Text.CompositeFormat CacheSummaryFormat = System.Text.CompositeFormat.Parse(GetString("InternalPage_IconCacheDiagnostics_CacheSummaryFormat"));
    private static readonly System.Text.CompositeFormat ProviderSummaryFormat = System.Text.CompositeFormat.Parse(GetString("InternalPage_IconCacheDiagnostics_ProviderSummaryFormat"));
    private static readonly System.Text.CompositeFormat ProviderTotalsFormat = System.Text.CompositeFormat.Parse(GetString("InternalPage_IconCacheDiagnostics_ProviderTotalsFormat"));

    private readonly IApplicationInfoService _appInfoService;
    private readonly IServiceProvider _serviceProvider;
    private IReadOnlyList<CachedIconProviderEntryViewModel> _cachedIconProviders = [];
    private string _cachedIconProvidersSummary = string.Empty;
    private string _garbageCollectionSummary = string.Empty;

    public InternalPageViewModel(IApplicationInfoService appInfoService, IServiceProvider serviceProvider)
    {
        _appInfoService = appInfoService;
        _serviceProvider = serviceProvider;

        GarbageCollectionSummary = GetString("InternalPage_IconCacheDiagnostics_GarbageCollectionNotRun");
        RefreshCachedIconProviders();
    }

    public IReadOnlyList<CachedIconProviderEntryViewModel> CachedIconProviders
    {
        get => _cachedIconProviders;
        private set => SetProperty(ref _cachedIconProviders, value);
    }

    public string CachedIconProvidersSummary
    {
        get => _cachedIconProvidersSummary;
        private set => SetProperty(ref _cachedIconProvidersSummary, value);
    }

    public string GarbageCollectionSummary
    {
        get => _garbageCollectionSummary;
        private set => SetProperty(ref _garbageCollectionSummary, value);
    }

    [RelayCommand]
    private void ThrowPlainMainThreadException()
    {
        Logger.LogDebug("Throwing test exception from the UI thread");
        throw new NotImplementedException("Test exception; thrown from the UI thread");
    }

    [RelayCommand]
    private void ThrowExceptionInUnobservedTask()
    {
        Logger.LogDebug("Starting a task that will throw test exception");
        Task.Run(() =>
        {
            Logger.LogDebug("Throwing test exception from a task");
            throw new InvalidOperationException("Test exception; thrown from a task");
        });
    }

    [RelayCommand]
    private void ThrowPlainMainThreadExceptionPii()
    {
        Logger.LogDebug("Throwing test exception from the UI thread (PII)");
        throw new InvalidOperationException(Microsoft.CmdPal.UI.Settings.InternalPage.SampleData.ExceptionMessageWithPii);
    }

    [RelayCommand]
    private async Task OpenLogsFolderAsync()
    {
        try
        {
            var logFolderPath = _appInfoService.LogDirectory;
            if (Directory.Exists(logFolderPath))
            {
                await Launcher.LaunchFolderPathAsync(logFolderPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open directory in Explorer", ex);
        }
    }

    [RelayCommand]
    private async Task OpenCurrentLogFileAsync()
    {
        try
        {
            var logPath = Logger.CurrentLogFile;
            if (File.Exists(logPath))
            {
                await Launcher.LaunchUriAsync(new Uri(logPath));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open log file", ex);
        }
    }

    [RelayCommand]
    private async Task OpenConfigFolderAsync()
    {
        try
        {
            var directory = _appInfoService.ConfigDirectory;
            if (Directory.Exists(directory))
            {
                await Launcher.LaunchFolderPathAsync(directory);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open directory in Explorer", ex);
        }
    }

    [RelayCommand]
    private void ToggleDevRibbon()
    {
        WeakReferenceMessenger.Default.Send(new ToggleDevRibbonMessage());
    }

    [RelayCommand]
    private void CollectGarbage()
    {
        var beforeBytes = GC.GetTotalMemory(forceFullCollection: false);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var afterBytes = GC.GetTotalMemory(forceFullCollection: false);

        UpdateGarbageCollectionSummary(beforeBytes, afterBytes);
        RefreshCachedIconProviders();
    }

    [RelayCommand]
    private void RefreshCachedIconProviders()
    {
        List<CachedIconProviderEntryViewModel> providers = [];
        long totalAdds = 0;
        long totalCapacity = 0;
        long totalCurrentCount = 0;
        long totalHits = 0;
        long totalMisses = 0;

        var iconSourceProviders = _serviceProvider.GetServices<IIconSourceProvider>();
        foreach (var provider in iconSourceProviders.OfType<CachedIconSourceProvider>())
        {
            var diagnostics = provider.GetDiagnostics();
            providers.Add(new CachedIconProviderEntryViewModel(
                diagnostics.Name,
                diagnostics.CacheStatistics,
                () => PruneCachedIconProvider(provider),
                diagnostics.IconSize));
            AddStatistics(diagnostics.CacheStatistics, ref totalAdds, ref totalCapacity, ref totalCurrentCount, ref totalHits, ref totalMisses);
        }

        var managedIconSourceFactory = _serviceProvider.GetService<ManagedIconSourceFactory>();
        if (managedIconSourceFactory is not null)
        {
            var diagnostics = managedIconSourceFactory.GetDiagnostics();
            foreach (var cache in diagnostics.Caches)
            {
                Action pruneAction = cache.Kind switch
                {
                    ManagedIconSourceFactoryCacheKind.StringIcons => () => PruneManagedStringIconCache(managedIconSourceFactory),
                    ManagedIconSourceFactoryCacheKind.Thumbnails => () => PruneManagedThumbnailCache(managedIconSourceFactory),
                    _ => RefreshCachedIconProviders,
                };

                providers.Add(new CachedIconProviderEntryViewModel(
                    cache.Name,
                    cache.CacheStatistics,
                    pruneAction));
                AddStatistics(cache.CacheStatistics, ref totalAdds, ref totalCapacity, ref totalCurrentCount, ref totalHits, ref totalMisses);
            }
        }

        providers.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        CachedIconProviders = providers;
        CachedIconProvidersSummary = providers.Count == 0
            ? GetString("InternalPage_IconCacheDiagnostics_NoProviders")
            : string.Format(
                GetCurrentCulture(),
                CachedProviderSummaryFormat,
                providers.Count,
                totalCurrentCount,
                totalCapacity,
                totalAdds,
                totalHits,
                totalMisses);
    }

    private void PruneCachedIconProvider(CachedIconSourceProvider provider)
    {
        provider.PruneCache();
        RefreshCachedIconProviders();
    }

    private void PruneManagedStringIconCache(ManagedIconSourceFactory factory)
    {
        factory.PruneStringIconCache();
        RefreshCachedIconProviders();
    }

    private void PruneManagedThumbnailCache(ManagedIconSourceFactory factory)
    {
        factory.PruneThumbnailIconCache();
        RefreshCachedIconProviders();
    }

    private void UpdateGarbageCollectionSummary(long beforeBytes, long afterBytes)
    {
        var deltaBytes = afterBytes - beforeBytes;
        GarbageCollectionSummary = string.Format(
            GetCurrentCulture(),
            GarbageCollectionSummaryFormat,
            beforeBytes,
            afterBytes,
            deltaBytes);
    }

    private static string GetString(string resourceName) => ResourceLoaderInstance.GetString(resourceName);

    private static IFormatProvider GetCurrentCulture() => System.Globalization.CultureInfo.CurrentCulture;

    private static string FormatSize(global::Windows.Foundation.Size size)
    {
        return string.Format(GetCurrentCulture(), "{0:0}x{1:0}", size.Width, size.Height);
    }

    private static void AddStatistics(
        AdaptiveCacheStatistics statistics,
        ref long totalAdds,
        ref long totalCapacity,
        ref long totalCurrentCount,
        ref long totalHits,
        ref long totalMisses)
    {
        totalAdds += statistics.AddCount;
        totalCapacity += statistics.Capacity;
        totalCurrentCount += statistics.Count;
        totalHits += statistics.HitCount;
        totalMisses += statistics.MissCount;
    }

    internal sealed class CachedIconProviderEntryViewModel
    {
        internal CachedIconProviderEntryViewModel(
            string name,
            AdaptiveCacheStatistics diagnostics,
            Action pruneAction,
            global::Windows.Foundation.Size? iconSize = null)
        {
            Name = name;
            Summary = iconSize is { } size && !size.IsEmpty
                ? string.Format(
                    GetCurrentCulture(),
                    ProviderSummaryFormat,
                    FormatSize(size),
                    diagnostics.Count,
                    diagnostics.Capacity,
                    diagnostics.PoolCount,
                    diagnostics.CurrentTick,
                    diagnostics.DecayInterval.TotalMinutes,
                    diagnostics.DecayFactor)
                : string.Format(
                    GetCurrentCulture(),
                    CacheSummaryFormat,
                    diagnostics.Count,
                    diagnostics.Capacity,
                    diagnostics.PoolCount,
                    diagnostics.CurrentTick,
                    diagnostics.DecayInterval.TotalMinutes,
                    diagnostics.DecayFactor);
            Totals = string.Format(
                GetCurrentCulture(),
                ProviderTotalsFormat,
                diagnostics.AddCount,
                diagnostics.HitCount,
                diagnostics.MissCount,
                diagnostics.RemoveCount,
                diagnostics.ClearCount,
                diagnostics.CleanupCount,
                diagnostics.CleanupEvictionCount);
            _pruneAction = pruneAction;
            PruneCommand = new RelayCommand(Prune);
        }

        public string Name { get; }

        public string Summary { get; }

        public string Totals { get; }

        public IRelayCommand PruneCommand { get; }

        private readonly Action _pruneAction;

        internal void Prune()
        {
            _pruneAction();
        }
    }
}

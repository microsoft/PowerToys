// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using CoreWidgetProvider.Helpers;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

public partial class PerformanceMonitorCommandsProvider : CommandProvider
{
    public const string ProviderIdValue = "PerformanceMonitor";
    public const string ProviderLoadGuardBlockId = ProviderIdValue + ".ProviderLoad";
    public const string PageIdValue = "com.microsoft.cmdpal.performanceWidget";

    private static readonly PerformanceMetricKind?[] BandMetrics =
    [
        null,
        PerformanceMetricKind.Cpu,
        PerformanceMetricKind.Memory,
        PerformanceMetricKind.Network,
        PerformanceMetricKind.Gpu,
        PerformanceMetricKind.Battery,
    ];

    internal static ProviderCrashSentinel CrashSentinel { get; } = new(ProviderIdValue);

    private readonly Lock _stateLock = new();
    private readonly SettingsManager _settingsManager = new();
    private ICommandItem[] _commands = [];
    private ICommandItem[] _bands = [];
    private PerformanceWidgetsPage? _mainPage;
    private PerformanceWidgetsPage? _bandPage;
    private PerformanceWidgetsPage? _cpuBandPage;
    private PerformanceWidgetsPage? _memoryBandPage;
    private PerformanceWidgetsPage? _diskBandPage;
    private PerformanceWidgetsPage? _networkBandPage;
    private PerformanceWidgetsPage? _gpuBandPage;
    private PerformanceWidgetsPage? _batteryBandPage;
    private bool _softDisabled;

    public PerformanceMonitorCommandsProvider(bool softDisabled = false)
    {
        DisplayName = Resources.GetResource("Performance_Monitor_Title");
        Id = ProviderIdValue;
        Icon = Icons.PerformanceMonitorIcon;

        Settings = _settingsManager.Settings;

        if (softDisabled)
        {
            SetDisabledState();
        }
        else
        {
            SetEnabledState();
        }
    }

    public override ICommandItem[] TopLevelCommands()
    {
        lock (_stateLock)
        {
            return _commands;
        }
    }

    public override ICommandItem[]? GetDockBands()
    {
        lock (_stateLock)
        {
            return _bands;
        }
    }

    public bool TryReactivateImmediately()
    {
        lock (_stateLock)
        {
            if (!_softDisabled)
            {
                return true;
            }

            try
            {
                CrashSentinel.ClearProviderState();
                SetEnabledState();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError("Failed to reactivate Performance Monitor in the current session. Keeping placeholder pages loaded.", ex);
                return false;
            }
        }

        RaiseItemsChanged();
        return true;
    }

    public override void Dispose()
    {
        lock (_stateLock)
        {
            DisposeActivePages();
        }

        GC.SuppressFinalize(this);
        base.Dispose();
    }

    private void SetDisabledState()
    {
        DisposeActivePages();

        var page = new PerformanceMonitorDisabledPage(this);

        // Mirror the compact form of the real bands, and keep texts short
        var disabledValue = Resources.GetResource("Performance_Monitor_Disabled_Band_Title");
        var bands = new List<ICommandItem>(BandMetrics.Length);
        foreach (var metric in BandMetrics)
        {
            var icon = GetBandIcon(metric);
            var item = new ListItem(page)
            {
                Title = disabledValue,
                Subtitle = GetBandSubtitle(metric),
                Icon = icon,
            };
            bands.Add(new WrappedDockItem([item], PerformanceWidgetsPage.GetBandId(metric), GetBandDisplayTitle(metric))
            {
                Icon = icon,
            });
        }

        _bands = bands.ToArray();
        _commands =
        [
            new CommandItem(page)
            {
                Title = DisplayName,
                Subtitle = Resources.GetResource("Performance_Monitor_Disabled_Subtitle"),
            },
        ];
        _softDisabled = true;
    }

    private string GetBandDisplayTitle(PerformanceMetricKind? metric)
    {
        return metric switch
        {
            PerformanceMetricKind.Cpu => Resources.GetResource("CPU_Usage_Title"),
            PerformanceMetricKind.Memory => Resources.GetResource("Memory_Usage_Title"),
            PerformanceMetricKind.Network => Resources.GetResource("Network_Usage_Title"),
            PerformanceMetricKind.Gpu => Resources.GetResource("GPU_Usage_Title"),
            PerformanceMetricKind.Battery => Resources.GetResource("Battery_Usage_Title"),
            _ => DisplayName,
        };
    }

    private string GetBandSubtitle(PerformanceMetricKind? metric)
    {
        return metric switch
        {
            PerformanceMetricKind.Cpu => Resources.GetResource("CPU_Usage_Subtitle"),
            PerformanceMetricKind.Memory => Resources.GetResource("Memory_Usage_Subtitle"),
            PerformanceMetricKind.Network => Resources.GetResource("Network_Usage_Subtitle"),
            PerformanceMetricKind.Gpu => Resources.GetResource("GPU_Usage_Subtitle"),
            PerformanceMetricKind.Battery => Resources.GetResource("Battery_Usage_Subtitle"),
            _ => string.Empty,
        };
    }

    private static IconInfo GetBandIcon(PerformanceMetricKind? metric)
    {
        return metric switch
        {
            PerformanceMetricKind.Cpu => Icons.CpuIcon,
            PerformanceMetricKind.Memory => Icons.MemoryIcon,
            PerformanceMetricKind.Network => Icons.NetworkIcon,
            PerformanceMetricKind.Gpu => Icons.GpuIcon,
            PerformanceMetricKind.Battery => Icons.BatteryIcon,
            _ => Icons.PerformanceMonitorIcon,
        };
    }

    private void SetEnabledState()
    {
        DisposeActivePages();

        _mainPage = new PerformanceWidgetsPage(_settingsManager, false);
        _bandPage = new PerformanceWidgetsPage(_settingsManager, true);
        _cpuBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Cpu);
        _memoryBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Memory);
        _networkBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Network);
        _diskBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Disk);
        _gpuBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Gpu);
        _batteryBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Battery);

        List<ICommandItem> bands = [
            new CommandItem(_bandPage) { Title = DisplayName },
            new CommandItem(_cpuBandPage) { Title = Resources.GetResource("CPU_Usage_Title") },
            new CommandItem(_memoryBandPage) { Title = Resources.GetResource("Memory_Usage_Title") },
            new CommandItem(_networkBandPage) { Title = Resources.GetResource("Network_Usage_Title") },
            new CommandItem(_diskBandPage) { Title = Resources.GetResource("Disk_Usage_Title") },
            new CommandItem(_gpuBandPage) { Title = Resources.GetResource("GPU_Usage_Title") }
        ];
        var batteryStats = new BatteryStats();
        batteryStats.GetData();
        if (batteryStats.HasBattery)
        {
            bands.Add(new CommandItem(_batteryBandPage) { Title = Resources.GetResource("Battery_Usage_Title") });
        }

        _bands = bands.ToArray();

        _commands =
        [
            new CommandItem(_mainPage)
            {
                Title = DisplayName,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];
        _softDisabled = false;
    }

    private void DisposeActivePages()
    {
        _mainPage?.Dispose();
        _mainPage = null;

        _bandPage?.Dispose();
        _bandPage = null;

        _cpuBandPage?.Dispose();
        _cpuBandPage = null;

        _memoryBandPage?.Dispose();
        _memoryBandPage = null;

        _diskBandPage?.Dispose();
        _diskBandPage = null;

        _networkBandPage?.Dispose();
        _networkBandPage = null;

        _gpuBandPage?.Dispose();
        _gpuBandPage = null;

        _batteryBandPage?.Dispose();
        _batteryBandPage = null;
    }
}

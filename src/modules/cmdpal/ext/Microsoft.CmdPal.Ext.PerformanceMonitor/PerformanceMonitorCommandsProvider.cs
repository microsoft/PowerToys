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

    internal static ProviderCrashSentinel CrashSentinel { get; } = new(ProviderIdValue);

    private readonly Lock _stateLock = new();
    private readonly SettingsManager _settingsManager = new();
    private ICommandItem[] _commands = [];
    private ICommandItem[] _bands = [];
    private PerformanceWidgetsPage? _mainPage;
    private PerformanceWidgetsPage? _bandPage;
    private PerformanceWidgetsPage? _cpuBandPage;
    private PerformanceWidgetsPage? _memoryBandPage;
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
        var band = new PerformanceMonitorDisabledPage(this);
        _bands =
        [
            new CommandItem(band)
            {
                Title = Resources.GetResource("Performance_Monitor_Disabled_Band_Title"),
                Subtitle = DisplayName,
            },
        ];
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

    private void SetEnabledState()
    {
        DisposeActivePages();

        _mainPage = new PerformanceWidgetsPage(_settingsManager, false);
        _bandPage = new PerformanceWidgetsPage(_settingsManager, true);
        _cpuBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Cpu);
        _memoryBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Memory);
        _networkBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Network);
        _gpuBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Gpu);
        _batteryBandPage = new PerformanceWidgetsPage(_settingsManager, true, PerformanceMetricKind.Battery);

        List<ICommandItem> bands = [
            new CommandItem(_bandPage) { Title = DisplayName },
            new CommandItem(_cpuBandPage) { Title = Resources.GetResource("CPU_Usage_Title") },
            new CommandItem(_memoryBandPage) { Title = Resources.GetResource("Memory_Usage_Title") },
            new CommandItem(_networkBandPage) { Title = Resources.GetResource("Network_Usage_Title") },
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

        _networkBandPage?.Dispose();
        _networkBandPage = null;

        _gpuBandPage?.Dispose();
        _gpuBandPage = null;

        _batteryBandPage?.Dispose();
        _batteryBandPage = null;
    }
}

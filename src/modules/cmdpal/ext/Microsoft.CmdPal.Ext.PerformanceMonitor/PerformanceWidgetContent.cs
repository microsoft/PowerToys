// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed partial class PerformanceWidgetContent : WidgetContent, IDisposable
{
    private readonly WidgetPage? _page;
    private readonly IFormContent? _form;
    private readonly Lock _activationLock = new();
    private bool _active;
    private bool _disposed;

    private PerformanceWidgetContent(PerformanceMetricKind metric, SettingsManager settingsManager, bool definitionOnly)
    {
        Id = GetId(metric);
        Title = GetTitle(metric);
        Description = GetDescription(metric);
        Icon = GetIcon(metric);
        SupportedSizes = [WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large];
        AllowMultiple = false;

        if (definitionOnly)
        {
            return;
        }

        _page = CreatePage(metric, settingsManager);
        _form = _page.GetContent().OfType<IFormContent>().First();
        Content = _form;
        _page.Updated += Page_Updated;
        _page.UpdateWidget();
    }

    public static IWidgetContent CreateDefinition(PerformanceMetricKind metric, SettingsManager settingsManager) =>
        new PerformanceWidgetContent(metric, settingsManager, definitionOnly: true);

    public static IWidgetContent? TryCreate(string id, string instanceId, SettingsManager settingsManager)
    {
        foreach (var metric in new[]
        {
            PerformanceMetricKind.Cpu,
            PerformanceMetricKind.Memory,
            PerformanceMetricKind.Disk,
            PerformanceMetricKind.Network,
            PerformanceMetricKind.Gpu,
            PerformanceMetricKind.Battery,
        })
        {
            if (string.Equals(GetId(metric), id, StringComparison.Ordinal))
            {
                return new PerformanceWidgetContent(metric, settingsManager, definitionOnly: false);
            }
        }

        return null;
    }

    public override void Activate()
    {
        lock (_activationLock)
        {
            if (_active || _page is null)
            {
                return;
            }

            _active = true;
            _page.PushActivate();
        }
    }

    public override void Deactivate()
    {
        lock (_activationLock)
        {
            if (!_active || _page is null)
            {
                return;
            }

            _active = false;
            _page.PopActivate();
        }
    }

    public override void Delete() => Dispose();

    private void Page_Updated(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Content));
    }

    private static WidgetPage CreatePage(PerformanceMetricKind metric, SettingsManager settingsManager) => metric switch
    {
        PerformanceMetricKind.Cpu => new SystemCPUUsageWidgetPage(),
        PerformanceMetricKind.Memory => new SystemMemoryUsageWidgetPage(),
        PerformanceMetricKind.Disk => new SystemDiskUsageWidgetPage(settingsManager),
        PerformanceMetricKind.Network => new SystemNetworkUsageWidgetPage(settingsManager),
        PerformanceMetricKind.Gpu => new SystemGPUUsageWidgetPage(),
        PerformanceMetricKind.Battery => new SystemBatteryUsageWidgetPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    private static string GetId(PerformanceMetricKind metric) => metric switch
    {
        PerformanceMetricKind.Cpu => "com.microsoft.cmdpal.widgets.performance.cpu",
        PerformanceMetricKind.Memory => "com.microsoft.cmdpal.widgets.performance.memory",
        PerformanceMetricKind.Disk => "com.microsoft.cmdpal.widgets.performance.disk",
        PerformanceMetricKind.Network => "com.microsoft.cmdpal.widgets.performance.network",
        PerformanceMetricKind.Gpu => "com.microsoft.cmdpal.widgets.performance.gpu",
        PerformanceMetricKind.Battery => "com.microsoft.cmdpal.widgets.performance.battery",
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    private static string GetTitle(PerformanceMetricKind metric) => metric switch
    {
        PerformanceMetricKind.Cpu => Resources.GetResource("CPU_Usage_Title"),
        PerformanceMetricKind.Memory => Resources.GetResource("Memory_Usage_Title"),
        PerformanceMetricKind.Disk => Resources.GetResource("Disk_Usage_Title"),
        PerformanceMetricKind.Network => Resources.GetResource("Network_Usage_Title"),
        PerformanceMetricKind.Gpu => Resources.GetResource("GPU_Usage_Title"),
        PerformanceMetricKind.Battery => Resources.GetResource("Battery_Usage_Title"),
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    private static string GetDescription(PerformanceMetricKind metric) => metric switch
    {
        PerformanceMetricKind.Cpu => Resources.GetResource("CPU_Usage_Subtitle"),
        PerformanceMetricKind.Memory => Resources.GetResource("Memory_Usage_Subtitle"),
        PerformanceMetricKind.Network => Resources.GetResource("Network_Usage_Subtitle"),
        PerformanceMetricKind.Gpu => Resources.GetResource("GPU_Usage_Subtitle"),
        PerformanceMetricKind.Battery => Resources.GetResource("Battery_Usage_Subtitle"),
        _ => GetTitle(metric),
    };

    private static IconInfo GetIcon(PerformanceMetricKind metric) => metric switch
    {
        PerformanceMetricKind.Cpu => Icons.CpuIcon,
        PerformanceMetricKind.Memory => Icons.MemoryIcon,
        PerformanceMetricKind.Disk => Icons.HardDriveIcon,
        PerformanceMetricKind.Network => Icons.NetworkIcon,
        PerformanceMetricKind.Gpu => Icons.GpuIcon,
        PerformanceMetricKind.Battery => Icons.BatteryIcon,
        _ => Icons.PerformanceMonitorIcon,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Deactivate();
        if (_page is not null)
        {
            _page.Updated -= Page_Updated;
        }

        (_page as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}

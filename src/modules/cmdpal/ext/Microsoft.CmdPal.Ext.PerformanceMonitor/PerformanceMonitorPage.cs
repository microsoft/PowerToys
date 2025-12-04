// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
///  asdfasdf
/// </summary>
/// <remarks>
/// Intentionally, we're using IListPage rather than ListPage. This is so we
/// can get the onload/onunload
/// </remarks>
internal sealed partial class PerformanceMonitorPage : OnLoadStaticPage, IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private readonly PerformanceCounter[]? _diskCounters;
    private readonly PerformanceCounter? _networkSentCounter;
    private readonly PerformanceCounter? _networkReceivedCounter;

    private readonly bool _isBandPage;

    // System performance data object to store all metrics
    private SystemPerformanceData _performanceData = new SystemPerformanceData();

    private int _loadCount;

    private bool IsActive => _loadCount > 0;

    private List<ListItem> _items = new List<ListItem>();
    private ListItem _cpuItem;
    private ListItem _memoryItem;
    private ListItem _diskItem;
    private ListItem? _networkItem;

    public override string Id => _isBandPage ? "com.crloewen.performanceMonitor.dockband" : "com.crloewen.PerformanceMonitor";

    public override string Title => "Performance monitor";

    public override string PlaceholderText => "Performance monitor";

    public override IconInfo Icon => new IconInfo("\uE9D2"); // switch

    private Task? _updateTask;

    // Start of code
    public PerformanceMonitorPage(bool asBandPage = false)
    {
        _isBandPage = asBandPage;

        // Initialize CPU counter
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue(); // First call always returns 0

        _cpuItem = new ListItem(new NoOpCommand() { Name = _isBandPage ? "CPU" : string.Empty })
        {
            Icon = new IconInfo("\uE9D9"), // CPU icon
            Title = "CPU",
            Details = new Details() { Body = "Loading..." },
        };
        _items.Add(_cpuItem);

        // Initialize Memory counter
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");

        _memoryItem = new ListItem(new NoOpCommand() { Name = _isBandPage ? "Memory" : string.Empty })
        {
            Icon = new IconInfo("\uE964"), // Memory icon
            Title = "Memory",
            Details = new Details() { Body = "Loading..." },
        };
        _items.Add(_memoryItem);

        // Initialize Disk counters (for all physical disks)
        var diskNames = GetPhysicalDiskNames();
        _diskCounters = new PerformanceCounter[diskNames.Length];
        for (var i = 0; i < diskNames.Length; i++)
        {
            _diskCounters[i] = new PerformanceCounter("PhysicalDisk", "% Disk Time", diskNames[i]);
            _diskCounters[i].NextValue(); // First call always returns 0
        }

        _diskItem = new ListItem(new NoOpCommand() { Name = _isBandPage ? "Disk" : string.Empty })
        {
            Icon = new IconInfo("\uE977"), // Disk icon
            Title = "Disk",
            Details = new Details() { Body = "Loading..." },
        };
        _items.Add(_diskItem);

        // Try to initialize Network counters (may not be available on all systems)
        try
        {
            var networkInterface = GetMostActiveNetworkInterface();
            if (!string.IsNullOrEmpty(networkInterface))
            {
                _networkSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", networkInterface);
                _networkReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", networkInterface);
                _networkSentCounter.NextValue(); // First call always returns 0
                _networkReceivedCounter.NextValue(); // First call always returns 0

                _networkItem = new ListItem(new NoOpCommand() { Name = _isBandPage ? "Network" : string.Empty })
                {
                    Icon = new IconInfo("\uEC05"), // Network icon
                    Title = "Network",
                    Details = new Details() { Body = "Loading..." },
                };

                _items.Add(_networkItem);
            }
        }
        catch (Exception)
        {
        }

        // Initialize performance data
        _performanceData.DiskInformation = GetDiskInformation();
    }

    protected override void Loaded()
    {
        _loadCount++;
        _updateTask ??= Task.Run(() =>
        {
            UpdateValues();
        });
    }

    protected override void Unloaded()
    {
        _loadCount--;

        // TODO! cancel the update task
    }

    private Details GetCPUDetails()
    {
        return new Details()
        {
            Title = "CPU Details",
            Body = $@"## Top CPU Processes

{_performanceData.TopCpuProcesses}

## Processor Information

- Number of Cores: {Environment.ProcessorCount}
- Architecture: {RuntimeInformation.ProcessArchitecture}",
        };
    }

    private Details GetMemoryDetails()
    {
        return new Details()
        {
            Title = "Memory Details",
            Body = $@"## Top Memory Processes

{_performanceData.TopMemoryProcesses}

## Memory info

- Total Physical Memory: {GetTotalPhysicalMemoryGB():0.00} GB
- Available Memory: {_performanceData.AvailableMemoryMB / 1024:0.00} GB
- Memory In Use: {GetUsedMemoryGB():0.00} GB",
        };
    }

    private Details GetDiskDetails()
    {
        return new Details()
        {
            Title = "Disk Details",
            Body = $@"## Top Disk Processes

{_performanceData.TopDiskProcesses}

## Disk Information

{_performanceData.DiskInformation}",
        };
    }

    private Details GetNetworkDetails()
    {
        return new Details()
        {
            Title = "Network Details",
            Body = $@"To be added in the future.",
        };
    }

    private async void UpdateValues()
    {
        // Update interval in milliseconds
        const int updateInterval = 500;

        // TODO: Fix this behaviour which is needed cause of a bug
        while (_loadCount > 0)
        {
            // Record start time of update cycle
            var startTime = DateTime.Now;

            var tasks = new List<Task>();

            // Start all update tasks in parallel
            if (_cpuItem != null)
            {
                tasks.Add(Task.Run(() => UpdateCpuValues()));
            }

            if (_memoryItem != null)
            {
                tasks.Add(Task.Run(() => UpdateMemoryValues()));
            }

            if (_diskCounters?.Length > 0 && _diskItem != null)
            {
                tasks.Add(Task.Run(() => UpdateDiskValues()));
            }

            if (_networkItem != null)
            {
                tasks.Add(Task.Run(() => UpdateNetworkValues()));
            }

            // tasks.Add(GetProcessInfo());

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Calculate how much time has passed
            var elapsedTime = (DateTime.Now - startTime).TotalMilliseconds;

            // If we completed faster than our desired interval, wait the remaining time
            if (elapsedTime < updateInterval)
            {
                await Task.Delay((int)(updateInterval - elapsedTime));
            }
        }
    }

    private void UpdateCpuValues()
    {
        if (_cpuCounter is null)
        {
            return;
        }

        // Quick update
        _performanceData.CurrentCpuUsage = _cpuCounter.NextValue();
        if (_isBandPage)
        {
            _cpuItem.Title = $"{_performanceData.CurrentCpuUsage:0.0}%";
            _cpuItem.Subtitle = "CPU";
        }
        else
        {
            _cpuItem.Title = $"CPU - {_performanceData.CurrentCpuUsage:0.0}%";
        }

        _cpuItem.Details = GetCPUDetails();
    }

    private void UpdateMemoryValues()
    {
        if (_memoryCounter is null)
        {
            return;
        }

        // Quick update
        _performanceData.AvailableMemoryMB = _memoryCounter.NextValue();
        _performanceData.CurrentMemoryUsage = 100f - (_performanceData.AvailableMemoryMB / GetTotalPhysicalMemory() * 100f);

        if (_isBandPage)
        {
            _memoryItem.Title = $"{_performanceData.CurrentMemoryUsage:0.0}%";
            _memoryItem.Subtitle = "Memory";
        }
        else
        {
            _memoryItem.Title = $"Memory - {_performanceData.CurrentMemoryUsage:0.0}%";
        }

        _memoryItem.Details = GetMemoryDetails();
    }

    private void UpdateDiskValues()
    {
        if (_diskCounters is null)
        {
            return;
        }

        // Quick update
        if (_diskCounters.Length > 0)
        {
            _performanceData.CurrentDiskUsage = _diskCounters.Average(counter => counter.NextValue());
        }

        if (_isBandPage)
        {
            _diskItem.Title = $"{_performanceData.CurrentDiskUsage:0.0}%";
            _diskItem.Subtitle = "Disk";
        }
        else
        {
            _diskItem.Title = $"Disk - {_performanceData.CurrentDiskUsage:0.0}%";
        }

        _diskItem.Details = GetDiskDetails();
    }

    private void UpdateNetworkValues()
    {
        if (_networkSentCounter is null ||
            _networkReceivedCounter is null ||
            _networkItem is null)
        {
            return;
        }

        // Quick update
        _performanceData.CurrentNetworkSentKBps = _networkSentCounter.NextValue() / 1024; // Convert to KB/s
        _performanceData.CurrentNetworkReceivedKBps = _networkReceivedCounter.NextValue() / 1024; // Convert to KB/s
        if (_isBandPage)
        {
            _networkItem.Title = $"{_performanceData.CurrentNetworkReceivedKBps:0.0} KB/s ↓, {_performanceData.CurrentNetworkSentKBps:0.0} KB/s ↑";
            _networkItem.Subtitle = "Network";
        }
        else
        {
            _networkItem.Title = $"Network - {_performanceData.CurrentNetworkReceivedKBps:0.0} KB/s ↓, {_performanceData.CurrentNetworkSentKBps:0.0} KB/s ↑";
        }

        _networkItem.Details = GetNetworkDetails();
    }

    public override IListItem[] GetItems()
    {
        return _items.ToArray();
    }

    // === Helper functions ===
    private async Task<bool> GetProcessInfo()
    {
        var pollingTime = 750;

        try
        {
            var initialProcessValues = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.ProcessName))
            .Select(p =>
            {
                try
                {
                    if (p.HasExited)
                    {
                        return null;
                    }

                    if (GetProcessIoCounters(p.Handle, out var counters))
                    {
                        var readVal = counters.ReadTransferCount;
                        var writeVal = counters.WriteTransferCount;
                        return new
                        {
                            Id = p.Id,
                            Name = p.ProcessName,
                            readVal,
                            writeVal,
                            totalProcessTime = p.TotalProcessorTime,
                            workingSet = p.WorkingSet64,
                        };
                    }
                }
                catch
                {
                    return null;
                }

                return null;
            })
            .Where(p => p != null)
            .Select(p => p!)
            .ToDictionary(p => p.Id, p => p);

            await Task.Delay(pollingTime); // Wait a bit to measure usage

            var finalProcessValues = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.ProcessName))
            .Select(p =>
            {
                try
                {
                    if (p.HasExited)
                    {
                        return null;
                    }

                    if (GetProcessIoCounters(p.Handle, out var counters))
                    {
                        var readVal = counters.ReadTransferCount;
                        var writeVal = counters.WriteTransferCount;
                        return new
                        {
                            Id = p.Id,
                            readVal,
                            writeVal,
                            totalProcessTime = p.TotalProcessorTime,
                        };
                    }
                }
                catch
                {
                    return null;
                }

                return null;
            })
            .Where(p => p != null)
            .Select(p => p!)
            .ToDictionary(p => p.Id, p => p);

            // Make new dictionary with finalizedProcesses
            var finalizedProcesses = new Dictionary<int, (string Name, ulong ReadVal, ulong WriteVal, double TotalProcessTime, long WorkingSet)>();

            foreach (var (key, value) in finalProcessValues)
            {
                if (value is null)
                {
                    continue;
                }

                if (initialProcessValues.TryGetValue(key, out var initialValue))
                {
                    var readVal = value.readVal - initialValue.readVal;
                    var writeVal = value.writeVal - initialValue.writeVal;
                    var totalProcessTime = (value.totalProcessTime - initialValue.totalProcessTime).TotalMilliseconds;
                    finalizedProcesses[key] = (initialValue.Name, readVal, writeVal, totalProcessTime, initialValue.workingSet);
                }
            }

            var secondConversion = 1000.0 / pollingTime;

            // Format the string for CPU usage
            var cpuString = new StringBuilder();

            var topCPUProcesses = finalizedProcesses
                .OrderByDescending(p => p.Value.TotalProcessTime)
                .Take(5)
                .ToDictionary(p => p.Key, p => p.Value);

            foreach (var (key, value) in topCPUProcesses)
            {
                var cpuUsage = value.TotalProcessTime * 100.0 / (pollingTime * Environment.ProcessorCount);
                cpuUsage = Math.Min(100, Math.Max(0, cpuUsage)); // Clamp between 0-100%
                var line = $"- {value.Name}: {cpuUsage:0.0}% CPU";
                cpuString.AppendLine(line);
            }

            _performanceData.TopCpuProcesses = cpuString.ToString();

            // Format the string for memory usage
            var memoryString = new StringBuilder();

            var topMemoryProcesses = finalizedProcesses
                .OrderByDescending(p => p.Value.WorkingSet)
                .Take(5)
                .ToDictionary(p => p.Key, p => p.Value);

            foreach (var (key, value) in topMemoryProcesses)
            {
                var line = $"- {value.Name}: {value.WorkingSet / 1024 / 1024:0.0} MB";
                memoryString.AppendLine(line);
            }

            _performanceData.TopMemoryProcesses = memoryString.ToString();

            // Format the string for disk usage
            var diskString = new StringBuilder();

            var topDiskProcesses = finalizedProcesses
                .OrderByDescending(p => p.Value.ReadVal)
                .Take(5)
                .ToDictionary(p => p.Key, p => p.Value);

            foreach (var (key, value) in topDiskProcesses)
            {
                var line = $"- {value.Name}: R: {value.ReadVal * secondConversion / 1024 / 1024:0.0} , W: {value.WriteVal * secondConversion / 1024 / 1024:0.0} MB";
                diskString.AppendLine(line);
            }

            _performanceData.TopDiskProcesses = diskString.ToString();
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private float GetTotalPhysicalMemoryGB()
    {
        return GetTotalPhysicalMemory() / 1024; // Convert MB to GB
    }

    private float GetUsedMemoryGB()
    {
        return (GetTotalPhysicalMemory() - _performanceData.AvailableMemoryMB) / 1024; // Convert MB to GB
    }

    private string GetDiskInformation()
    {
        var result = new System.Text.StringBuilder();

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var freeSpaceGB = drive.TotalFreeSpace / (1024.0 * 1024 * 1024);
            var totalSpaceGB = drive.TotalSize / (1024.0 * 1024 * 1024);
            var usedPercent = 100 - (freeSpaceGB / totalSpaceGB * 100);

            var usedBlocks = (int)(usedPercent / 10);

            var body = $"""
### Drive {drive.Name} ({drive.VolumeLabel}):

{freeSpaceGB:0.00} GB free of {totalSpaceGB:0.00} GB

{usedPercent:0.0}% used

\\[{new string('⬛', usedBlocks)}{new string('⬜', 10 - usedBlocks)}\\]
""";

            result.AppendLine(body);
        }

        return result.ToString();
    }

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public byte localPort1;
        public byte localPort2;
        public byte localPort3;
        public byte localPort4;
        public uint remoteAddr;
        public byte remotePort1;
        public byte remotePort2;
        public byte remotePort3;
        public byte remotePort4;
        public int owningPid;
    }
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        int tblClass,
        int reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

    private static string[] GetPhysicalDiskNames()
    {
        var category = new PerformanceCounterCategory("PhysicalDisk");
        var instanceNames = category.GetInstanceNames();
        return instanceNames.Where(name => name != "_Total").ToArray();
    }

    private static string? GetMostActiveNetworkInterface()
    {
        var category = new PerformanceCounterCategory("Network Interface");
        return category.GetInstanceNames().FirstOrDefault(name => !name.Contains("Loopback"));
    }

    private static float GetTotalPhysicalMemory()
    {
        return (float)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024); // Convert bytes to MB
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();

        if (_diskCounters != null)
        {
            foreach (var counter in _diskCounters)
            {
                counter?.Dispose();
            }
        }

        _networkSentCounter?.Dispose();
        _networkReceivedCounter?.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct IO_COUNTERS
{
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
}

internal abstract partial class OnLoadStaticPage : Page, IListPage
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _showDetails;
    private bool _hasMore;
    private IFilters? _filters;
    private IGridProperties? _gridProperties;
    private ICommandItem? _emptyContent;
    private int _loadCount;

#pragma warning disable CS0067 // The event is never used

    private event TypedEventHandler<object, IItemsChangedEventArgs>? InternalItemsChanged;
#pragma warning restore CS0067 // The event is never used

    public event TypedEventHandler<object, IItemsChangedEventArgs> ItemsChanged
    {
        add
        {
            InternalItemsChanged += value;
            if (_loadCount == 0)
            {
                Loaded();
            }

            _loadCount++;
        }

        remove
        {
            InternalItemsChanged -= value;
            _loadCount--;
            _loadCount = Math.Max(0, _loadCount);
            if (_loadCount == 0)
            {
                Unloaded();
            }
        }
    }

    protected abstract void Loaded();

    protected abstract void Unloaded();

    public virtual string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value;
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    public virtual string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
        }
    }

    public virtual bool ShowDetails
    {
        get => _showDetails;
        set
        {
            _showDetails = value;
            OnPropertyChanged(nameof(ShowDetails));
        }
    }

    public virtual bool HasMoreItems
    {
        get => _hasMore;
        set
        {
            _hasMore = value;
            OnPropertyChanged(nameof(HasMoreItems));
        }
    }

    public virtual IFilters? Filters
    {
        get => _filters;
        set
        {
            _filters = value;
            OnPropertyChanged(nameof(Filters));
        }
    }

    public virtual IGridProperties? GridProperties
    {
        get => _gridProperties;
        set
        {
            _gridProperties = value;
            OnPropertyChanged(nameof(GridProperties));
        }
    }

    public virtual ICommandItem? EmptyContent
    {
        get => _emptyContent;
        set
        {
            _emptyContent = value;
            OnPropertyChanged(nameof(EmptyContent));
        }
    }

    public void LoadMore()
    {
    }

    protected void SetSearchNoUpdate(string newSearchText)
    {
        _searchText = newSearchText;
    }

    public abstract IListItem[] GetItems();
}

#pragma warning restore SA1402 // File may only contain a single type

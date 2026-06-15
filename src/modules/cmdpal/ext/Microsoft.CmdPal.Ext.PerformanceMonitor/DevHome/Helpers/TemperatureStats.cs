// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace CoreWidgetProvider.Helpers;

// Reads CPU temperature from the "Thermal Zone Information" PDH category (ACPI thermal zones).
// Raw counter values are in tenths of Kelvin; we convert to Celsius on read.
// Not available on all systems (e.g. VMs without ACPI thermal zones) - check IsAvailable first.
internal sealed partial class TemperatureStats : PerformanceCounterSourceBase
{
    private const string CategoryName = "Thermal Zone Information";
    private const string CounterName = "High Precision Temperature";

    // Tenths of Kelvin -> Celsius: (raw - 2731.5) / 10
    private const double TenthsKelvinOffset = 2731.5;

    private readonly PerformanceCounter? _thermalCounter;
    private bool _readFailureLogged;

    public bool IsAvailable => _thermalCounter is not null;

    /// <summary>Gets the last sampled CPU thermal zone temperature in °C, or -1 if unavailable.</summary>
    public double CpuTemperatureCelsius { get; private set; } = -1;

    public TemperatureStats()
    {
        var category = CreatePerformanceCounterCategory(CategoryName, logFailure: false);
        if (category is null)
        {
            return;
        }

        var instances = category.GetInstanceNames();
        if (instances.Length == 0)
        {
            return;
        }

        // Prefer standard ACPI thermal zone instances (_TZ.*), fall back to the first available.
        var preferred = Array.Find(instances, n => n.StartsWith("_TZ.", StringComparison.OrdinalIgnoreCase))
            ?? instances[0];

        _thermalCounter = CreatePerformanceCounter(CategoryName, CounterName, preferred, logFailure: false);
    }

    public void GetData()
    {
        if (_thermalCounter is null)
        {
            CpuTemperatureCelsius = -1;
            return;
        }

        try
        {
            var raw = _thermalCounter.NextValue();
            CpuTemperatureCelsius = (raw - TenthsKelvinOffset) / 10.0;
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _readFailureLogged, $"Failed to read {CategoryName}\\{CounterName}.", ex);
            CpuTemperatureCelsius = -1;
        }
    }
}

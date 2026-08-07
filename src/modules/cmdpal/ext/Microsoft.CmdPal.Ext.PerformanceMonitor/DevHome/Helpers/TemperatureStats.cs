// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace CoreWidgetProvider.Helpers;

// Reads temperature from the "Thermal Zone Information" PDH category (ACPI thermal zones).
// Raw counter values are in tenths of Kelvin; we convert to Celsius on read.
// Not available on all systems (e.g. VMs without ACPI thermal zones) - check IsAvailable first.
// Note: ACPI thermal zones often reflect an ambient/skin/motherboard sensor, not a CPU die.
internal sealed partial class TemperatureStats : PerformanceCounterSourceBase, IDisposable
{
    private const string CategoryName = "Thermal Zone Information";
    private const string CounterName = "High Precision Temperature";

    // Tenths of Kelvin -> Celsius: (raw - 2731.5) / 10
    private const double TenthsKelvinOffset = 2731.5;

    // Plausibility range: reject readings outside this window as sensor noise or bad samples.
    private const double MinPlausibleCelsius = -20.0;
    private const double MaxPlausibleCelsius = 150.0;

    private readonly PerformanceCounter? _thermalCounter;
    private bool _readFailureLogged;

    public bool IsAvailable => _thermalCounter is not null;

    /// <summary>Gets the last sampled thermal zone temperature in °C, or -1 if unavailable or out of range.</summary>
    public double TemperatureCelsius { get; private set; } = -1;

    public TemperatureStats()
    {
        try
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
        catch (Exception ex)
        {
            LogFailureOnce(ref _readFailureLogged, $"Failed to initialize {CategoryName} performance counter.", ex);
        }
    }

    public void GetData()
    {
        if (_thermalCounter is null)
        {
            TemperatureCelsius = -1;
            return;
        }

        try
        {
            var raw = _thermalCounter.NextValue();
            var celsius = (raw - TenthsKelvinOffset) / 10.0;

            TemperatureCelsius = celsius >= MinPlausibleCelsius && celsius <= MaxPlausibleCelsius
                ? celsius
                : -1;
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _readFailureLogged, $"Failed to read {CategoryName}\\{CounterName}.", ex);
            TemperatureCelsius = -1;
        }
    }

    public void Dispose()
    {
        _thermalCounter?.Dispose();
    }
}

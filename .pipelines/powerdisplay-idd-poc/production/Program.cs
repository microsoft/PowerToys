// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.IddPoc.ProductionProbe;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            // Capture production Logger output without the application's COM-dependent log-path setup.
            using var listener = new TextWriterTraceListener(Console.Error);
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var manager = new MonitorManager(new ObservationStore());
            var inventory = DisplayConfigInventory.GetAllMonitorDisplayInfo().Values
                .Where(info => IsFixture(info.DevicePath))
                .OrderBy(info => info.DevicePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var discovered = (await manager.DiscoverMonitorsAsync(timeout.Token))
                .Where(monitor => IsFixture(monitor.Id))
                .OrderBy(monitor => monitor.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var rediscovered = (await manager.DiscoverMonitorsAsync(timeout.Token))
                .Where(monitor => IsFixture(monitor.Id))
                .OrderBy(monitor => monitor.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var passed = inventory.Length == 2 && discovered.Length == 2 &&
                discovered.All(monitor => monitor.SupportsBrightness) && rediscovered.All(monitor => monitor.SupportsBrightness) &&
                discovered.Select(monitor => monitor.Id).SequenceEqual(rediscovered.Select(monitor => monitor.Id));
            using var assembly = File.OpenRead(typeof(MonitorManager).Assembly.Location);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Passed = passed,
                Completed = true,
                ProductionAssembly = typeof(MonitorManager).Assembly.Location,
                ProductionAssemblySha256 = Convert.ToHexString(SHA256.HashData(assembly)),
                Inventory = inventory.Select(info => new { info.DevicePath, info.FriendlyName, info.GdiDeviceName }),
                Discovered = discovered.Select(monitor => new
                {
                    monitor.Id,
                    monitor.Name,
                    PhysicalHandle = monitor.Handle.ToInt64(),
                    monitor.SupportsBrightness,
                    monitor.CurrentBrightness,
                    monitor.CommunicationMethod,
                }),
                Rediscovered = rediscovered.Select(monitor => monitor.Id),
                Error = passed ? null : "Production discovery did not retain two brightness-capable fixture monitors with stable IDs.",
            }));
            return passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { Passed = false, Completed = false, Error = exception.ToString() }));
            return 1;
        }
    }

    private static bool IsFixture(string id) =>
        id.Contains("DISPLAY#DELD0E6#", StringComparison.OrdinalIgnoreCase) ||
        id.Contains("DISPLAY#LEN65BF#", StringComparison.OrdinalIgnoreCase);

    private sealed class ObservationStore : IKnownGoodVcpStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<byte, KnownGoodVcpFeature>> _observations =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId) =>
            _observations.GetOrAdd(monitorId, static _ => new ConcurrentDictionary<byte, KnownGoodVcpFeature>());

        public void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature)
        {
            _ = GetKnownGoodFeatures(monitorId);
            _observations[monitorId][feature.Code] = feature.Clone();
        }
    }
}

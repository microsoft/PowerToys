// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class MonitorStateManagerTests
{
    private const string MonitorA = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1";
    private const string MonitorB = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID2";
    private static readonly DateTime SuccessfulUtc = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);

    private string _directory = null!;
    private string _statePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"PowerDisplayState-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _statePath = Path.Combine(_directory, "monitor_state.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void KnownGoodFeatures_RoundTripPreservesObservation()
    {
        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpsertKnownGoodFeature(
                MonitorA,
                new KnownGoodVcpFeature
                {
                    Code = 0x10,
                    Current = 30,
                    Maximum = 100,
                    Source = VcpObservationSource.MaximumCompatibilityProbe,
                    LastSuccessfulUtc = SuccessfulUtc,
                });
        }

        using var reloaded = new MonitorStateManager(_statePath);
        var features = reloaded.GetKnownGoodFeatures(MonitorA);

        Assert.AreEqual(1, features.Count);
        Assert.AreEqual(30, features[0x10].Current);
        Assert.AreEqual(100, features[0x10].Maximum);
        Assert.AreEqual(VcpObservationSource.MaximumCompatibilityProbe, features[0x10].Source);
        Assert.AreEqual(SuccessfulUtc, features[0x10].LastSuccessfulUtc);
    }

    [TestMethod]
    public void ControllerDerivedCacheKey_MatchesMonitorIdAndSurvivesRetentionRoundTrip()
    {
        const string rawDevicePath =
            @"\\?\DISPLAY#AOCB326#5&2f1a4f2&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var displayInfo = new MonitorDisplayInfo
        {
            DevicePath = rawDevicePath,
            FriendlyName = "AOC Q27G3XMN",
            GdiDeviceName = @"\\.\DISPLAY1",
            MonitorNumber = 1,
        };
        var canonicalId = MonitorIdentity.FromDevicePath(rawDevicePath);
        var controllerCacheKey = DdcCiController.DeriveMonitorId(displayInfo);

        Assert.AreEqual(canonicalId, controllerCacheKey);
        Assert.AreNotEqual(rawDevicePath, controllerCacheKey);

        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(canonicalId, "Brightness", 35);
            manager.UpsertKnownGoodFeature(controllerCacheKey, Feature(0x10, current: 35));
            manager.RetainMonitorStates(new[] { canonicalId });
        }

        using (var document = JsonDocument.Parse(File.ReadAllText(_statePath)))
        {
            var monitors = document.RootElement.GetProperty("monitors");
            Assert.IsTrue(monitors.TryGetProperty(canonicalId, out _));
            Assert.IsFalse(monitors.TryGetProperty(rawDevicePath, out _));
        }

        using var reloaded = new MonitorStateManager(_statePath);
        Assert.AreEqual(35, reloaded.GetMonitorParameters(canonicalId)?.Brightness);
        Assert.AreEqual(35, reloaded.GetKnownGoodFeatures(canonicalId)[0x10].Current);
        Assert.IsNull(reloaded.GetMonitorParameters(rawDevicePath));
        Assert.AreEqual(0, reloaded.GetKnownGoodFeatures(rawDevicePath).Count);
    }

    [TestMethod]
    public void GetKnownGoodFeatures_UsesExactDevicePathComparer()
    {
        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(
            MonitorA,
            new KnownGoodVcpFeature
            {
                Code = 0x10,
                Current = 40,
                Maximum = 100,
                Source = VcpObservationSource.CapabilitiesInitialization,
                LastSuccessfulUtc = SuccessfulUtc,
            });

        Assert.AreEqual(1, manager.GetKnownGoodFeatures(MonitorA.ToLowerInvariant()).Count);
        Assert.AreEqual(0, manager.GetKnownGoodFeatures(MonitorB).Count);
    }

    [TestMethod]
    public void UpsertKnownGoodFeature_ReplacesOnlyMatchingCode()
    {
        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20));
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x12, current: 60));
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 35));

        var features = manager.GetKnownGoodFeatures(MonitorA);
        Assert.AreEqual(2, features.Count);
        Assert.AreEqual(35, features[0x10].Current);
        Assert.AreEqual(60, features[0x12].Current);
    }

    [TestMethod]
    public void RetainMonitorStates_RemovesEntireUnretainedMonitorEntry()
    {
        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(MonitorA, "Brightness", 25);
            manager.UpdateMonitorParameter(MonitorB, "Contrast", 80);
            manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20));
            manager.UpsertKnownGoodFeature(MonitorB, Feature(0x10, current: 80));

            manager.RetainMonitorStates(new[] { MonitorA });
        }

        using (var document = JsonDocument.Parse(File.ReadAllText(_statePath)))
        {
            var monitors = document.RootElement.GetProperty("monitors");
            Assert.IsTrue(monitors.TryGetProperty(MonitorA, out _));
            Assert.IsFalse(monitors.TryGetProperty(MonitorB, out _));
        }

        using var reloaded = new MonitorStateManager(_statePath);
        Assert.AreEqual(25, reloaded.GetMonitorParameters(MonitorA)?.Brightness);
        Assert.IsNull(reloaded.GetMonitorParameters(MonitorB));
        Assert.AreEqual(1, reloaded.GetKnownGoodFeatures(MonitorA).Count);
        Assert.AreEqual(0, reloaded.GetKnownGoodFeatures(MonitorB).Count);
    }

    [TestMethod]
    public void RetainMonitorStates_CaseInsensitiveRetainedIdKeepsState()
    {
        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20));

        manager.RetainMonitorStates(new[] { MonitorA.ToLowerInvariant() });

        Assert.AreEqual(1, manager.GetKnownGoodFeatures(MonitorA).Count);
    }

    [TestMethod]
    public void ConcurrentUpserts_PreserveBothMonitorEntries()
    {
        using var manager = new MonitorStateManager(_statePath);

        Parallel.Invoke(
            () => manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20)),
            () => manager.UpsertKnownGoodFeature(MonitorB, Feature(0x10, current: 80)));

        Assert.AreEqual(20, manager.GetKnownGoodFeatures(MonitorA)[0x10].Current);
        Assert.AreEqual(80, manager.GetKnownGoodFeatures(MonitorB)[0x10].Current);
    }

    [TestMethod]
    public void RetainMonitorStates_EmptySetClearsCompleteState()
    {
        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(MonitorA, "Volume", 20);
            manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20));

            manager.RetainMonitorStates(Array.Empty<string>());
        }

        using (var document = JsonDocument.Parse(File.ReadAllText(_statePath)))
        {
            var monitors = document.RootElement.GetProperty("monitors");
            Assert.IsFalse(monitors.TryGetProperty(MonitorA, out _));
        }

        using var reloaded = new MonitorStateManager(_statePath);
        Assert.IsNull(reloaded.GetMonitorParameters(MonitorA));
        Assert.AreEqual(0, reloaded.GetKnownGoodFeatures(MonitorA).Count);
    }

    [TestMethod]
    public void Load_OldStateWithoutKnownGoodFeatures_UsesEmptyCollection()
    {
        const string stateJson =
            """
            {"monitors":{"MONITOR-A":{"brightness":42,"lastUpdated":"2026-07-21T08:00:00Z"}},"lastUpdated":"2026-07-21T08:00:00Z"}
            """;
        File.WriteAllText(_statePath, stateJson);

        using var manager = new MonitorStateManager(_statePath);

        Assert.AreEqual(0, manager.GetKnownGoodFeatures("MONITOR-A").Count);
        Assert.AreEqual(42, manager.GetMonitorParameters("MONITOR-A")?.Brightness);
    }

    private static KnownGoodVcpFeature Feature(byte code, int current) => new()
    {
        Code = code,
        Current = current,
        Maximum = 100,
        Source = VcpObservationSource.MaximumCompatibilityProbe,
        LastSuccessfulUtc = SuccessfulUtc,
    };
}

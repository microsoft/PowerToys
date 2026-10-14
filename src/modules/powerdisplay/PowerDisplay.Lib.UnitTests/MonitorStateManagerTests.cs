// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A debounced SaveStateToDisk can still hold the state file when the test's
            // using-block returns: Dispose cancels the debouncer, but a save already past its
            // delay is inside WriteStateFile with no token to observe. Leaving a temp
            // directory behind is not worth failing an otherwise green test over.
        }
        catch (UnauthorizedAccessException)
        {
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
                });
        }

        using var reloaded = new MonitorStateManager(_statePath);
        var features = reloaded.GetKnownGoodFeatures(MonitorA);

        Assert.AreEqual(1, features.Count);
        Assert.AreEqual(30, features[0x10].Current);
        Assert.AreEqual(100, features[0x10].Maximum);
    }

    [TestMethod]
    public void DeriveMonitorId_ReturnsTheCanonicalIdTheStateFileIsKeyedBy()
    {
        // The controller's known-good cache key must be the canonical Id MonitorStateManager stores
        // under, otherwise a probe writes evidence that retention can never find or collect.
        const string rawDevicePath =
            @"\\?\DISPLAY#AOCB326#5&2f1a4f2&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

        var controllerCacheKey = DdcCiController.DeriveMonitorId(
            new MonitorDisplayInfo { DevicePath = rawDevicePath, FriendlyName = "AOC Q27G3XMN" });

        Assert.AreEqual(MonitorIdentity.FromDevicePath(rawDevicePath), controllerCacheKey);
        Assert.AreNotEqual(rawDevicePath, controllerCacheKey);
    }

    [TestMethod]
    public void GetKnownGoodFeatures_UsesExactDevicePathComparer()
    {
        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 40));

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
    public void MigrateLegacyKeys_MergesMissingFieldsAndFeaturesIntoCanonicalState()
    {
        const string legacyId = "DDC_AOCB326_1";

        using var manager = new MonitorStateManager(_statePath);
        manager.UpdateMonitorParameter(legacyId, "Brightness", 42);
        manager.UpdateMonitorParameter(legacyId, "Contrast", 65);
        manager.UpsertKnownGoodFeature(legacyId, Feature(0x10, current: 42));
        manager.UpsertKnownGoodFeature(legacyId, Feature(0x12, current: 65));

        manager.UpdateMonitorParameter(MonitorA, "Brightness", 55);
        manager.UpdateMonitorParameter(MonitorA, "Volume", 25);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 55));

        manager.MigrateLegacyKeys(new[] { (MonitorA, 1) });

        var parameters = manager.GetMonitorParameters(MonitorA);
        Assert.AreEqual(55, parameters?.Brightness);
        Assert.AreEqual(65, parameters?.Contrast);
        Assert.AreEqual(25, parameters?.Volume);

        var features = manager.GetKnownGoodFeatures(MonitorA);
        Assert.AreEqual(2, features.Count);
        Assert.AreEqual(55, features[0x10].Current);
        Assert.AreEqual(65, features[0x12].Current);
        Assert.IsNull(manager.GetMonitorParameters(legacyId));
        Assert.AreEqual(0, manager.GetKnownGoodFeatures(legacyId).Count);
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

    [TestMethod]
    public void UpsertKnownGoodFeature_UnchangedValueDoesNotScheduleASave()
    {
        // Every discovery pass re-reads all three continuous codes, so re-observing an unchanged
        // value is the common case and must not rewrite the whole state file. Deleting the file and
        // checking Dispose does not recreate it is the observable form of "never marked dirty":
        // Dispose only flushes when _isDirty was set, and loading from disk does not set it.
        using (var seed = new MonitorStateManager(_statePath))
        {
            seed.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 40));
        }

        Assert.IsTrue(File.Exists(_statePath), "The seeding upsert is a real change and must persist.");

        using (var manager = new MonitorStateManager(_statePath))
        {
            File.Delete(_statePath);
            manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 40));
        }

        Assert.IsFalse(File.Exists(_statePath), "An unchanged re-observation must not dirty the state.");
    }

    [TestMethod]
    public void UpsertKnownGoodFeature_ChangedValueSchedulesASave()
    {
        // The control for UpsertKnownGoodFeature_UnchangedValueDoesNotScheduleASave: same shape,
        // one different Current, and the flush must happen.
        using (var seed = new MonitorStateManager(_statePath))
        {
            seed.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 40));
        }

        using (var manager = new MonitorStateManager(_statePath))
        {
            File.Delete(_statePath);
            manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 41));
        }

        Assert.IsTrue(File.Exists(_statePath));

        using var reloaded = new MonitorStateManager(_statePath);
        Assert.AreEqual(41, reloaded.GetKnownGoodFeatures(MonitorA)[0x10].Current);
    }

    private static KnownGoodVcpFeature Feature(byte code, int current) => new()
    {
        Code = code,
        Current = current,
        Maximum = 100,
    };
}

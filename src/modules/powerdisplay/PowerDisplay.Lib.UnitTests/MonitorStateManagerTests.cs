// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
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
                    LastSuccessfulUtc = SuccessfulUtc,
                });
        }

        using var reloaded = new MonitorStateManager(_statePath);
        var features = reloaded.GetKnownGoodFeatures(MonitorA);

        Assert.AreEqual(1, features.Count);
        Assert.AreEqual(30, features[0x10].Current);
        Assert.AreEqual(100, features[0x10].Maximum);
        Assert.AreEqual(SuccessfulUtc, features[0x10].LastSuccessfulUtc);
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
    public void RemoveKnownGoodFeatures_ClearsCacheButKeepsSavedUserValues()
    {
        // MonitorA is never named in the removal list, so it also stands for the case where
        // reconciliation observed no drop at all and nothing may be collected.
        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(MonitorA, "Brightness", 25);
            manager.UpdateMonitorParameter(MonitorA, "Volume", 20);
            manager.UpdateMonitorParameter(MonitorB, "Contrast", 80);
            manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20));
            manager.UpsertKnownGoodFeature(MonitorB, Feature(0x10, current: 80));

            manager.RemoveKnownGoodFeatures(new[] { MonitorB });
        }

        using var reloaded = new MonitorStateManager(_statePath);

        // MonitorB's entry itself survives: only the discovery cache this feature owns is collected.
        Assert.AreEqual(25, reloaded.GetMonitorParameters(MonitorA)?.Brightness);
        Assert.AreEqual(20, reloaded.GetMonitorParameters(MonitorA)?.Volume);
        Assert.AreEqual(80, reloaded.GetMonitorParameters(MonitorB)?.Contrast);
        Assert.AreEqual(1, reloaded.GetKnownGoodFeatures(MonitorA).Count);
        Assert.AreEqual(0, reloaded.GetKnownGoodFeatures(MonitorB).Count);
    }

    [TestMethod]
    public void RemoveKnownGoodFeatures_MatchesIdsCaseInsensitively()
    {
        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 20));

        manager.RemoveKnownGoodFeatures(new[] { MonitorA.ToLowerInvariant() });

        Assert.AreEqual(0, manager.GetKnownGoodFeatures(MonitorA).Count);
    }

    [TestMethod]
    public void RemoveKnownGoodFeatures_LegacyEntryRemainsAvailableForMigration()
    {
        const string legacyId = "DDC_AOCB326_1";

        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(legacyId, "Brightness", 42);
            manager.UpsertKnownGoodFeature(legacyId, Feature(0x10, current: 42));

            manager.RemoveKnownGoodFeatures(new[] { legacyId });
            Assert.AreEqual(42, manager.GetMonitorParameters(legacyId)?.Brightness);
            Assert.AreEqual(1, manager.GetKnownGoodFeatures(legacyId).Count);

            manager.MigrateLegacyKeys(new[] { (MonitorA, 1) });
            Assert.AreEqual(42, manager.GetMonitorParameters(MonitorA)?.Brightness);
            Assert.IsNull(manager.GetMonitorParameters(legacyId));
        }

        using var reloaded = new MonitorStateManager(_statePath);
        Assert.AreEqual(42, reloaded.GetMonitorParameters(MonitorA)?.Brightness);
        Assert.IsNull(reloaded.GetMonitorParameters(legacyId));
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
    public void UpsertKnownGoodFeature_UnchangedValueStillRefreshesTheInMemoryTimestamp()
    {
        // The save short-circuit must not cost the timestamp its meaning: the support log reads
        // LastSuccessfulUtc to report when the hardware last answered for a code, so an unchanged
        // re-observation still has to move it even though it does not earn a disk write.
        var later = SuccessfulUtc.AddHours(6);

        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 40));
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 40, observedUtc: later));

        Assert.AreEqual(later, manager.GetKnownGoodFeatures(MonitorA)[0x10].LastSuccessfulUtc);
    }

    [TestMethod]
    public void UpsertKnownGoodFeature_UnchangedValueDoesNotScheduleASave()
    {
        // Every discovery pass re-reads all three continuous codes, so re-observing an unchanged
        // value is the common case and must not rewrite the whole state file for a moved timestamp.
        // Deleting the file and checking Dispose does not recreate it is the observable form of
        // "never marked dirty": Dispose only flushes when _isDirty was set, and loading from disk
        // does not set it.
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

    [TestMethod]
    public void ConcurrentUpsertAndRead_OnSameMonitorDoNotTearTheFeatureMap()
    {
        // Drives the contention `lock (state)` exists for: a single monitor Id, whose one
        // MonitorState is the lock object shared by the discovery thread upserting several VCP
        // codes and the debounced save enumerating that same KnownGoodVcpFeatures dictionary.
        // Remove the locks and the reader's ToDictionary inside GetKnownGoodFeatures observes a
        // dictionary whose version changed mid-enumeration, and throws InvalidOperationException.
        //
        // Two different monitor Ids would not reproduce it: GetOrAdd maps them to two MonitorState
        // instances and therefore two disjoint locks.
        using var manager = new MonitorStateManager(_statePath);
        manager.UpsertKnownGoodFeature(MonitorA, Feature(0x10, current: 10));

        // 20 rounds x 64 codes is already ~1300 interleaved operations, which reproduces the tear
        // reliably. Every upsert that changes a value also resets the save debouncer, cancelling the
        // pending Task.Delay with an OperationCanceledException, so a larger count buys nothing but
        // throw churn and first-chance exception noise under a debugger.
        const int Rounds = 20;
        Exception? readerFailure = null;
        using var start = new Barrier(2);

        var writer = new Thread(() =>
        {
            start.SignalAndWait();
            for (var round = 0; round < Rounds; round++)
            {
                for (byte code = 0x20; code < 0x60; code++)
                {
                    manager.UpsertKnownGoodFeature(MonitorA, Feature(code, current: code));
                }

                manager.RemoveKnownGoodFeatures(new[] { MonitorA });
            }
        })
        {
            IsBackground = true,
        };

        var reader = new Thread(() =>
        {
            start.SignalAndWait();
            try
            {
                for (var round = 0; round < Rounds; round++)
                {
                    foreach (var pair in manager.GetKnownGoodFeatures(MonitorA))
                    {
                        Assert.AreEqual(100, pair.Value.Maximum);
                    }
                }
            }
            catch (Exception ex)
            {
                readerFailure = ex;
            }
        })
        {
            IsBackground = true,
        };

        writer.Start();
        reader.Start();

        Assert.IsTrue(writer.Join(TimeSpan.FromSeconds(30)), "Writer thread did not finish.");
        Assert.IsTrue(reader.Join(TimeSpan.FromSeconds(30)), "Reader thread did not finish.");
        Assert.IsNull(readerFailure, $"Concurrent read observed a torn feature map: {readerFailure}");
    }

    private static KnownGoodVcpFeature Feature(byte code, int current, DateTime? observedUtc = null) => new()
    {
        Code = code,
        Current = current,
        Maximum = 100,
        LastSuccessfulUtc = observedUtc ?? SuccessfulUtc,
    };
}

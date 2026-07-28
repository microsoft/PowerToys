// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers the wiring that turns <see cref="DdcCiController.MaxCompatibilityMode"/> into evidence:
/// whether the probe runs, whether the known-good cache is consulted, and whether probe successes
/// are persisted. <c>VcpDiscoveryEvidenceTests</c> covers what Reconcile does with that evidence;
/// these tests cover the controller lines that decide what it is handed.
/// </summary>
/// <remarks>
/// <see cref="IntPtr.Zero"/> is a safe handle to drive this with: DdcCiNative.TryGetCapabilitiesString
/// short-circuits on it without issuing a native call, so the capabilities string is deterministically
/// unusable and every remaining decision comes from the injected reader, clock and store.
/// </remarks>
[TestClass]
public sealed class DdcCiControllerCompatibilityGateTests
{
    private const string MonitorId = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1";
    private const int VcpNotSupported = unchecked((int)0xC0262584);

    [TestMethod]
    public async Task NormalMode_NeitherProbesNorTouchesTheCache()
    {
        var store = new RecordingStore(Cached(0x10, current: 45, maximum: 100));
        var reader = new ScriptedReader();
        using var controller = NewController(store, reader, maxCompatibility: false);

        var evidence = await controller.FetchCapabilitiesWithFallbackAsync(
            IntPtr.Zero, MonitorId, CancellationToken.None);

        Assert.AreEqual(0, reader.CallCount, "Normal mode must not probe VCP features.");
        Assert.AreEqual(0, store.GetCallCount, "Normal mode must not consult the known-good cache.");
        Assert.AreEqual(0, store.UpsertCount);
        Assert.IsNull(evidence.Capabilities);
        Assert.IsFalse(evidence.IsPhysicalMonitorUnavailable);
    }

    [TestMethod]
    public async Task MaxCompatibility_PersistsProbeSuccessesAndConsultsTheCache()
    {
        var store = new RecordingStore();
        var reader = new ScriptedReader
        {
            [0x10] = VcpReadAttempt.Success(30, 100),
            [0x12] = VcpReadAttempt.Failure(VcpNotSupported),
            [0x62] = VcpReadAttempt.Failure(VcpNotSupported),
        };
        var clock = new FixedClock();
        using var controller = NewController(store, reader, maxCompatibility: true, clock);

        var evidence = await controller.FetchCapabilitiesWithFallbackAsync(
            IntPtr.Zero, MonitorId, CancellationToken.None);

        Assert.AreEqual(1, store.GetCallCount, "Maximum compatibility mode must consult the cache.");
        Assert.AreEqual(1, store.UpsertCount, "Only the successful probe may be persisted.");

        var persisted = store.Upserts[0];
        Assert.AreEqual((byte)0x10, persisted.Code);
        Assert.AreEqual(30, persisted.Current);
        Assert.AreEqual(100, persisted.Maximum);
        Assert.AreEqual(VcpObservationSource.MaximumCompatibilityProbe, persisted.Source);
        Assert.AreEqual(clock.UtcNow, persisted.LastSuccessfulUtc);

        Assert.IsTrue(evidence.Capabilities!.SupportsVcpCode(0x10));
        Assert.IsTrue(evidence.InitialValues[0x10].IsLive);
    }

    [TestMethod]
    public async Task MaxCompatibility_CacheSupplementsAFailedProbe()
    {
        // Every probe fails, so the only thing keeping the monitor discoverable is the cache the
        // gate handed to Reconcile — this is the path the whole feature exists for.
        var store = new RecordingStore(Cached(0x10, current: 45, maximum: 100));
        var reader = new ScriptedReader
        {
            [0x10] = VcpReadAttempt.Failure(VcpNotSupported),
            [0x12] = VcpReadAttempt.Failure(VcpNotSupported),
            [0x62] = VcpReadAttempt.Failure(VcpNotSupported),
        };
        using var controller = NewController(store, reader, maxCompatibility: true);

        var evidence = await controller.FetchCapabilitiesWithFallbackAsync(
            IntPtr.Zero, MonitorId, CancellationToken.None);

        Assert.AreEqual(0, store.UpsertCount, "A failed probe must not be persisted.");
        Assert.IsTrue(evidence.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(45, evidence.InitialValues[0x10].Value.Current);
        Assert.IsFalse(evidence.InitialValues[0x10].IsLive);
    }

    private static DdcCiController NewController(
        RecordingStore store,
        ScriptedReader reader,
        bool maxCompatibility,
        ISystemClock? clock = null) =>
        new(store, clock ?? new FixedClock(), reader, (_, _) => Task.CompletedTask)
        {
            MaxCompatibilityMode = maxCompatibility,
        };

    private static KnownGoodVcpFeature Cached(byte code, int current, int maximum) => new()
    {
        Code = code,
        Current = current,
        Maximum = maximum,
        Source = VcpObservationSource.MaximumCompatibilityProbe,
        LastSuccessfulUtc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc),
    };

    private sealed class ScriptedReader : IVcpFeatureReader
    {
        private readonly Dictionary<byte, VcpReadAttempt> _results = new();

        public int CallCount { get; private set; }

        public VcpReadAttempt this[byte code]
        {
            set => _results[code] = value;
        }

        public VcpReadAttempt Read(IntPtr handle, byte code)
        {
            CallCount++;
            return _results.TryGetValue(code, out var result)
                ? result
                : VcpReadAttempt.Failure(VcpNotSupported);
        }
    }

    private sealed class RecordingStore : IKnownGoodVcpStore
    {
        private readonly Dictionary<byte, KnownGoodVcpFeature> _cached = new();

        public RecordingStore(params KnownGoodVcpFeature[] cached)
        {
            foreach (var feature in cached)
            {
                _cached[feature.Code] = feature;
            }
        }

        public int GetCallCount { get; private set; }

        public int UpsertCount => Upserts.Count;

        public List<KnownGoodVcpFeature> Upserts { get; } = new();

        public IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId)
        {
            GetCallCount++;
            return _cached;
        }

        public void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature) =>
            Upserts.Add(feature.Clone());
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTime UtcNow { get; } = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);
    }
}

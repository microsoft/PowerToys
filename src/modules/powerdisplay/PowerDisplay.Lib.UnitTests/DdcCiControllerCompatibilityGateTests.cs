// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using static PowerDisplay.UnitTests.DdcFakes;

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
/// unusable and every remaining decision comes from the injected reader and store.
/// </remarks>
[TestClass]
public sealed class DdcCiControllerCompatibilityGateTests
{
    [TestMethod]
    public async Task NormalMode_NeitherProbesNorTouchesTheCache()
    {
        var store = new RecordingKnownGoodStore(Cached(0x10, current: 45, maximum: 100));
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
        var store = new RecordingKnownGoodStore();
        var reader = new ScriptedReader
        {
            [0x10] = VcpReadAttempt.Success(30, 100),
            [0x12] = VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
            [0x62] = VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
        };
        using var controller = NewController(store, reader, maxCompatibility: true);

        var evidence = await controller.FetchCapabilitiesWithFallbackAsync(
            IntPtr.Zero, MonitorId, CancellationToken.None);

        Assert.AreEqual(1, store.GetCallCount, "Maximum compatibility mode must consult the cache.");
        Assert.AreEqual(1, store.UpsertCount, "Only the successful probe may be persisted.");

        var persisted = store.Upserts[0];
        Assert.AreEqual((byte)0x10, persisted.Code);
        Assert.AreEqual(30, persisted.Current);
        Assert.AreEqual(100, persisted.Maximum);

        Assert.IsTrue(evidence.Capabilities!.SupportsVcpCode(0x10));
        Assert.IsTrue(evidence.InitialValues[0x10].IsLive);
    }

    [TestMethod]
    public async Task MaxCompatibility_CacheSupplementsAFailedProbe()
    {
        // Every probe fails, so the only thing keeping the monitor discoverable is the cache the
        // gate handed to Reconcile — this is the path the whole feature exists for.
        var store = new RecordingKnownGoodStore(Cached(0x10, current: 45, maximum: 100));
        var reader = new ScriptedReader
        {
            [0x10] = VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
            [0x12] = VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
            [0x62] = VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
        };
        using var controller = NewController(store, reader, maxCompatibility: true);

        var evidence = await controller.FetchCapabilitiesWithFallbackAsync(
            IntPtr.Zero, MonitorId, CancellationToken.None);

        Assert.AreEqual(0, store.UpsertCount, "A failed probe must not be persisted.");
        Assert.IsTrue(evidence.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(45, evidence.InitialValues[0x10].Value.Current);
        Assert.IsFalse(evidence.InitialValues[0x10].IsLive);
    }

    [TestMethod]
    public async Task MaxCompatibility_ReadingsTakenBeforeTheHandleDiedAreStillPersisted()
    {
        // The probe stops at the first handle-class failure, so 0x10 was answered while the handle
        // was still live: the value is proven even though the pass is not. Reconcile discards the
        // evidence to keep BuildMonitorFromPhysical from publishing a monitor behind a dead handle,
        // which is a different question from whether the reading was real — and a dead handle
        // usually means a cable or KVM switch, so the next rediscovery is exactly the pass that
        // wants this value back.
        var store = new RecordingKnownGoodStore();
        var reader = new ScriptedReader
        {
            [0x10] = VcpReadAttempt.Success(30, 100),
            [0x12] = VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists),
        };
        using var controller = NewController(store, reader, maxCompatibility: true);

        var evidence = await controller.FetchCapabilitiesWithFallbackAsync(
            IntPtr.Zero, MonitorId, CancellationToken.None);

        Assert.IsTrue(evidence.IsPhysicalMonitorUnavailable);
        Assert.IsNull(evidence.Capabilities, "The pass itself is discarded...");
        Assert.AreEqual(0, evidence.InitialValues.Count);

        Assert.AreEqual(1, store.UpsertCount, "...but the reading that preceded the death is not.");
        Assert.AreEqual((byte)0x10, store.Upserts[0].Code);
        Assert.AreEqual(30, store.Upserts[0].Current);
    }

    private static DdcCiController NewController(
        RecordingKnownGoodStore store,
        ScriptedReader reader,
        bool maxCompatibility) =>
        new(store, reader, (_, _) => Task.CompletedTask)
        {
            MaxCompatibilityMode = maxCompatibility,
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
                : VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported);
        }
    }
}

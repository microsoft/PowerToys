// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Test doubles and fixtures shared by the DDC discovery tests. They live nested inside one
/// container so the file keeps a single top-level type; call sites pull them into scope with
/// <c>using static PowerDisplay.UnitTests.DdcFakes;</c> and use them unqualified.
/// </summary>
internal static class DdcFakes
{
    /// <summary>
    /// A canonical DevicePath-shaped monitor Id. Every test that needs one uses this so a change to
    /// what <c>MonitorIdentity</c> considers canonical shows up in one place.
    /// </summary>
    internal const string MonitorId = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1";

    /// <summary>
    /// A cache entry as a completed probe would have written it.
    /// </summary>
    internal static KnownGoodVcpFeature Cached(byte code, int current, int maximum) => new()
    {
        Code = code,
        Current = current,
        Maximum = maximum,
    };

    /// <summary>
    /// Serves a scripted sequence of read results and records what it was asked for, so a test can
    /// pin both how many native reads happened and against which codes.
    /// </summary>
    /// <remarks>
    /// Dequeuing past the end throws rather than yielding a default-valued result, so a fabricated
    /// reply never reaches the assertions. The throw is not itself the failure message: it is
    /// raised inside the reader, and <see cref="VcpFeatureProbeService"/>'s catch-all turns it into
    /// an indeterminate observation. An extra read is named by the <see cref="CallCount"/> and
    /// <see cref="Codes"/> assertions instead.
    /// </remarks>
    internal sealed class RecordingVcpReader(params VcpReadAttempt[] results) : IVcpFeatureReader
    {
        private readonly Queue<VcpReadAttempt> _results = new(results);

        public int CallCount { get; private set; }

        public List<byte> Codes { get; } = new();

        public VcpReadAttempt Read(IntPtr handle, byte code)
        {
            CallCount++;
            Codes.Add(code);
            return _results.Dequeue();
        }
    }

    /// <summary>
    /// An in-memory known-good store that records every call and lets an upsert be read back.
    /// </summary>
    /// <remarks>
    /// The monitor Id is deliberately ignored on both operations: matching on it here would
    /// silently stand in for the production guards a test means to pin. <see cref="GetCallCount"/>
    /// therefore also lets a test prove the caller short-circuited before reaching the store at all.
    /// </remarks>
    internal sealed class RecordingKnownGoodStore : IKnownGoodVcpStore
    {
        private readonly Dictionary<byte, KnownGoodVcpFeature> _features = new();

        public RecordingKnownGoodStore(params KnownGoodVcpFeature[] seed)
        {
            foreach (var feature in seed)
            {
                _features[feature.Code] = feature;
            }
        }

        public int GetCallCount { get; private set; }

        public int UpsertCount => Upserts.Count;

        public List<KnownGoodVcpFeature> Upserts { get; } = new();

        public KnownGoodVcpFeature? LastFeature => Upserts.Count > 0 ? Upserts[^1] : null;

        public IReadOnlyDictionary<byte, KnownGoodVcpFeature> GetKnownGoodFeatures(string monitorId)
        {
            GetCallCount++;
            return _features;
        }

        public void UpsertKnownGoodFeature(string monitorId, KnownGoodVcpFeature feature)
        {
            var stored = feature.Clone();
            Upserts.Add(stored);
            _features[stored.Code] = stored;
        }
    }
}

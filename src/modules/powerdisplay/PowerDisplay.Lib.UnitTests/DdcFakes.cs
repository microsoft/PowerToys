// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Drivers.DDC;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Test doubles and fixtures shared by the DDC discovery tests. They live nested inside one
/// container so the file keeps a single top-level type; call sites pull them into scope with
/// <c>using static PowerDisplay.UnitTests.DdcFakes;</c> and use them unqualified.
/// </summary>
internal static class DdcFakes
{
    /// <summary>
    /// A canonical DevicePath-shaped monitor Id, so a change to what a canonical Id looks like
    /// shows up in one place.
    /// </summary>
    internal const string MonitorId = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1";

    /// <summary>
    /// Serves a scripted sequence of read results and records what it was asked for, so a test can
    /// pin both how many native reads happened and against which codes.
    /// </summary>
    /// <remarks>
    /// Dequeuing past the end throws rather than yielding a default-valued result, so a fabricated
    /// reply never reaches the assertions. The throw is not itself the failure message: an extra
    /// read is named by the <see cref="CallCount"/> and <see cref="Codes"/> assertions instead.
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
}

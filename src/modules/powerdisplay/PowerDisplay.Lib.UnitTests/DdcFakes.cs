// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Drivers.DDC;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Test doubles shared by the DDC discovery tests. They live nested inside one container so the
/// file keeps a single top-level type; call sites pull them into scope with
/// <c>using static PowerDisplay.UnitTests.DdcFakes;</c> and use them unqualified.
/// </summary>
internal static class DdcFakes
{
    /// <summary>
    /// Serves a scripted sequence of read results and records what it was asked for, so a test can
    /// pin both how many native reads happened and against which codes.
    /// </summary>
    /// <remarks>
    /// Dequeuing past the end throws, which is deliberate: an extra read is a regression, and a
    /// hard failure names it better than a default-valued result flowing on into the assertions.
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

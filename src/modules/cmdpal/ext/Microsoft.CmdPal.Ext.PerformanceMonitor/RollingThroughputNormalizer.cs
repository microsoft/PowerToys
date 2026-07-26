// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class RollingThroughputNormalizer
{
    internal static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    internal const double HeadroomMultiplier = 1.2;
    internal const double MinimumScaleBytesPerSecond = 125_000;

    private readonly Queue<Sample> _samples = new();
    private readonly Lock _samplesLock = new();

    internal int AddSample(double bytesPerSecond, DateTimeOffset timestamp) =>
        AddPairSample(bytesPerSecond, 0, timestamp).FirstPercent;

    internal ThroughputPairPercentages AddPairSample(
        double firstBytesPerSecond,
        double secondBytesPerSecond,
        DateTimeOffset timestamp)
    {
        var firstThroughput = NormalizeThroughput(firstBytesPerSecond);
        var secondThroughput = NormalizeThroughput(secondBytesPerSecond);
        var currentPeak = Math.Max(firstThroughput, secondThroughput);

        lock (_samplesLock)
        {
            var cutoff = timestamp - Window;
            while (_samples.Count > 0 && _samples.Peek().Timestamp <= cutoff)
            {
                _samples.Dequeue();
            }

            _samples.Enqueue(new(timestamp, currentPeak));

            var rollingPeak = 0.0;
            foreach (var sample in _samples)
            {
                rollingPeak = Math.Max(rollingPeak, sample.BytesPerSecond);
            }

            var scale = Math.Max(MinimumScaleBytesPerSecond, rollingPeak * HeadroomMultiplier);
            return new(
                ToPercent(firstThroughput, scale),
                ToPercent(secondThroughput, scale));
        }
    }

    private static double NormalizeThroughput(double bytesPerSecond) =>
        double.IsFinite(bytesPerSecond) ? Math.Max(0, bytesPerSecond) : 0;

    private static int ToPercent(double bytesPerSecond, double scale) =>
        Math.Clamp(
            (int)Math.Round(bytesPerSecond * 100 / scale, MidpointRounding.AwayFromZero),
            0,
            100);

    private readonly record struct Sample(DateTimeOffset Timestamp, double BytesPerSecond);

    internal readonly record struct ThroughputPairPercentages(int FirstPercent, int SecondPercent);
}

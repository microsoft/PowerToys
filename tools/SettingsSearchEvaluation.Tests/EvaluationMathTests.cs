// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SettingsSearchEvaluation.Tests;

[TestClass]
public class EvaluationMathTests
{
    private static readonly double[] LatencySamples = { 10.0, 20.0, 30.0, 40.0, 50.0 };

    [TestMethod]
    public void FindBestRank_ReturnsExpectedRank()
    {
        var ranked = new[] { "a", "b", "c", "d" };
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c" };

        var rank = EvaluationMath.FindBestRank(ranked, expected);

        Assert.AreEqual(3, rank);
    }

    [TestMethod]
    public void FindBestRank_ReturnsZero_WhenMissing()
    {
        var ranked = new[] { "a", "b", "c", "d" };
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x", "y" };

        var rank = EvaluationMath.FindBestRank(ranked, expected);

        Assert.AreEqual(0, rank);
    }

    [TestMethod]
    public void ComputeLatencySummary_ComputesQuantiles()
    {
        var summary = EvaluationMath.ComputeLatencySummary(LatencySamples);

        Assert.AreEqual(5, summary.Samples);
        Assert.AreEqual(10.0, summary.MinMs);
        Assert.AreEqual(30.0, summary.P50Ms);
        Assert.AreEqual(50.0, summary.P95Ms);
        Assert.AreEqual(50.0, summary.MaxMs);
        Assert.AreEqual(30.0, summary.AverageMs, 0.0001);
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class VisualAssertTests
{
    [TestMethod]
    [DataRow(93.75, false)]
    [DataRow(94.99, false)]
    [DataRow(95.0, true)]
    [DataRow(100.0, true)]
    public void DefaultSimilarityThresholdRemains95(double similarity, bool expected)
    {
        Assert.AreEqual(expected, VisualAssert.IsSimilar(similarity));
    }

    [TestMethod]
    [DataRow(92.99, 93, false)]
    [DataRow(93.0, 93, true)]
    [DataRow(93.75, 93, true)]
    [DataRow(93.75, 95, false)]
    [DataRow(0.99, 1, false)]
    [DataRow(1.0, 1, true)]
    [DataRow(99.99, 100, false)]
    [DataRow(100.0, 100, true)]
    public void ExplicitSimilarityThresholdUsesInclusiveBoundary(double similarity, int threshold, bool expected)
    {
        Assert.AreEqual(expected, VisualAssert.IsSimilar(similarity, threshold));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(101)]
    public void InvalidSimilarityThresholdIsRejected(int threshold)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => VisualAssert.IsSimilar(100, threshold));
    }
}

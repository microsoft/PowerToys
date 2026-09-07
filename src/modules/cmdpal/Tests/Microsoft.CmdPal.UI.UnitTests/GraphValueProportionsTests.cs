// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.Graphs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class GraphValueProportionsTests
{
    private static readonly double[] EmptyPair = [0, 0];
    private static readonly double[] EqualPair = [0.5, 0.5];
    private static readonly double[] Values = [0, 24, 4, 4, 0];
    private static readonly double[] Proportions = [0, 0.75, 0.125, 0.125, 0];

    [TestMethod]
    public void Normalize_PreservesZerosAndProportionsWithoutMutatingInput()
    {
        double[] values = [0, 24, 4, 4, 0];
        var result = GraphValueProportions.Normalize(values);
        Assert.HasCount(Proportions.Length, result);
        for (var index = 0; index < result.Length; index++)
        {
            Assert.AreEqual(Proportions[index], result[index], 1e-12);
        }

        Assert.AreEqual(0d, result[0]);
        Assert.AreEqual(0d, result[^1]);
        CollectionAssert.AreEqual(Values, values);
    }

    [TestMethod]
    [DataRow(double.MaxValue)]
    [DataRow(double.Epsilon)]
    public void Normalize_HandlesValuesWhoseUnscaledTotalIsUnsafe(double value)
    {
        CollectionAssert.AreEqual(EqualPair, GraphValueProportions.Normalize([value, value]));
    }

    [TestMethod]
    public void Normalize_LeavesEmptyAndAllZeroBarsEmpty()
    {
        Assert.IsEmpty(GraphValueProportions.Normalize([]));
        CollectionAssert.AreEqual(EmptyPair, GraphValueProportions.Normalize([0, 0]));
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class IncompleteQueryTests
{
    [DataTestMethod]
    [DataRow("2+2+", "2+2")]
    [DataRow("2+2*", "2+2")]
    [DataRow("sin(30", "sin(30)")]
    [DataRow("((1+2)", "((1+2))")]
    [DataRow("2*(3+4", "2*(3+4)")]
    [DataRow("(1+2", "(1+2)")]
    [DataRow("2*(", "2")]
    [DataRow("2*(((", "2")]
    public void TestTryGetIncompleteQuerySuccess(string input, string expected)
    {
        var result = QueryHelper.TryGetIncompleteQuery(input, out var newQuery);
        Assert.IsTrue(result);
        Assert.AreEqual(expected, newQuery);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void TestTryGetIncompleteQueryFail(string input)
    {
        var result = QueryHelper.TryGetIncompleteQuery(input, out var newQuery);
        Assert.IsFalse(result);
        Assert.AreEqual(input, newQuery);
    }
}

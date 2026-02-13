// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class QueryHelperTests
{
    [DataTestMethod]
    [DataRow("2²", "4")]
    [DataRow("2³", "8")]
    [DataRow("2！", "2")]
    [DataRow("2\u00A0*\u00A02", "4")] // Non-breaking space
    [DataRow("20:10", "2")] // Colon as division
    public void Interpret_HandlesNormalizedInputs(string input, string expected)
    {
        var settings = new Settings();
        var result = QueryHelper.Query(input, settings, false, out _, (_, _) => { });

        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Title);
    }
}

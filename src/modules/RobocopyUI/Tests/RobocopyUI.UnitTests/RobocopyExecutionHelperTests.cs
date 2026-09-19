// Copyright (c) Microsoft Corporation.
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RobocopyUI.Helpers;

namespace RobocopyUI.UnitTests;

[TestClass]
public class RobocopyExecutionHelperTests
{
    [DataTestMethod]
    [DataRow(0, "Status_0")]
    [DataRow(3, "Status_3")]
    [DataRow(4, "Status_4")]
    [DataRow(7, "Status_7")]
    [DataRow(8, "Status_8")]
    [DataRow(9, "Status_8")]
    [DataRow(12, "Status_8")]
    [DataRow(16, "Status_Fail")]
    [DataRow(24, "Status_Fail")]
    public void GetStatusResourceKey_MapsRobocopyExitCodes(int exitCode, string expectedKey)
    {
        Assert.AreEqual(expectedKey, RobocopyExecutionHelper.GetStatusResourceKey(exitCode));
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class ExitCodeMatrixTests
{
    [DataTestMethod]
    [DataRow(CliExitCodes.Ok, 0)]
    [DataRow(CliExitCodes.MonitorNotFound, 1)]
    [DataRow(CliExitCodes.OutOfRange, 2)]
    [DataRow(CliExitCodes.InvalidDiscreteValue, 3)]
    [DataRow(CliExitCodes.UnsupportedFeature, 4)]
    [DataRow(CliExitCodes.HardwareFailure, 5)]
    [DataRow(CliExitCodes.SelectorMissing, 6)]
    [DataRow(CliExitCodes.ArgumentError, 7)]
    [DataRow(CliExitCodes.Timeout, 8)]
    [DataRow(CliExitCodes.InternalError, 9)]
    public void ExitCodes_AreStable(int actual, int expected) => Assert.AreEqual(expected, actual);
}

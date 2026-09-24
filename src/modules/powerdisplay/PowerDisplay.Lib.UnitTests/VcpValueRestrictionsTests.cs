// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpValueRestrictionsTests
{
    private const string AffectedMonitor = @"\\?\DISPLAY#SAM105C#5&abc&0&UID111";
    private const string OtherMonitor = @"\\?\DISPLAY#DELD1A8#5&abc&0&UID222";

    [TestMethod]
    [DataRow(@"\\?\DISPLAY#SAM105C#5&abc&0&UID111")]
    [DataRow(@"\\?\display#sam105c#5&xyz&0&uid222")]
    [DataRow(@"\\?\DISPLAY#SAM105C#5&xyz&0&UID333#{guid}")]
    public void HardwareRule_MatchesModelRegardlessOfCaseOrInstance(string monitorId)
    {
        Assert.IsTrue(VcpValueRestrictions.IsBlockedByHardware(monitorId, 0xD6, 0x05));
        StringAssert.Contains(
            VcpValueRestrictions.GetHardwareBlockReason(monitorId, 0xD6, 0x05)!,
            "https://github.com/microsoft/PowerToys/issues/50449");
    }

    [TestMethod]
    public void HardwareRule_OnlyBlocksTheSpecifiedModelCodeAndValue()
    {
        Assert.IsTrue(VcpValueRestrictions.IsBlockedByHardware(AffectedMonitor, 0xD6, 0x05));
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(OtherMonitor, 0xD6, 0x05));
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(AffectedMonitor, 0xD6, 0x01));
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(AffectedMonitor, 0xD6, 0x04));
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(AffectedMonitor, 0x14, 0x05));
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(AffectedMonitor, 0xD6, 0x105));
        Assert.IsNull(VcpValueRestrictions.GetHardwareBlockReason(AffectedMonitor, 0xD6, 0x04));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("SAM105C")]
    [DataRow(@"\\?\DISPLAY#SAM105C")]
    [DataRow(@"\\?\DISPLAY##instance")]
    public void HardwareRule_UnrecognizedMonitorIdDoesNotMatch(string? monitorId)
    {
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(monitorId!, 0xD6, 0x05));
        Assert.IsNull(VcpValueRestrictions.GetHardwareBlockReason(monitorId!, 0xD6, 0x05));
    }

    [TestMethod]
    public void UserRules_SupportMultipleCodesAndValuesWithoutTruncatingToAByte()
    {
        var userBlocks = new List<VcpValueBlock>
        {
            new() { VcpCode = 0x60, Values = new List<int> { 0x11, 0x12 } },
            new() { VcpCode = 0xE0, Values = new List<int> { 0x1234 } },
        };

        Assert.IsTrue(VcpValueRestrictions.IsBlocked(OtherMonitor, 0x60, 0x11, userBlocks));
        Assert.IsTrue(VcpValueRestrictions.IsBlocked(OtherMonitor, 0x60, 0x12, userBlocks));
        Assert.IsTrue(VcpValueRestrictions.IsBlocked(OtherMonitor, 0xE0, 0x1234, userBlocks));
        Assert.IsFalse(VcpValueRestrictions.IsBlocked(OtherMonitor, 0x60, 0x0F, userBlocks));
        Assert.IsFalse(VcpValueRestrictions.IsBlocked(OtherMonitor, 0xE0, 0x34, userBlocks));
        Assert.IsFalse(VcpValueRestrictions.IsBlocked(OtherMonitor, 0xE1, 0x1234, userBlocks));
    }

    [TestMethod]
    public void CombinedRules_AreAUnionAndUserRulesCannotOverrideHardwareRules()
    {
        var userBlocks = new List<VcpValueBlock>
        {
            new() { VcpCode = 0xD6, Values = new List<int> { 0x04 } },
        };

        Assert.IsTrue(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x04, userBlocks));
        Assert.IsTrue(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x05, userBlocks));
        Assert.IsFalse(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x01, userBlocks));
        Assert.IsFalse(VcpValueRestrictions.IsBlockedByHardware(AffectedMonitor, 0xD6, 0x04));

        userBlocks.Clear();

        Assert.IsTrue(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x05, userBlocks));
        Assert.IsFalse(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x04, userBlocks));
        Assert.IsTrue(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x05, null));
    }

    [TestMethod]
    public void UserRules_NullListsEntriesAndValuesAreSafe()
    {
        var userBlocks = new List<VcpValueBlock>
        {
            null!,
            new() { VcpCode = 0xD6, Values = null! },
        };

        Assert.IsFalse(VcpValueRestrictions.IsBlocked(OtherMonitor, 0xD6, 0x05, null));
        Assert.IsFalse(VcpValueRestrictions.IsBlocked(OtherMonitor, 0xD6, 0x05, userBlocks));
        Assert.IsTrue(VcpValueRestrictions.IsBlocked(AffectedMonitor, 0xD6, 0x05, userBlocks));
    }
}

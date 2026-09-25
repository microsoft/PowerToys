// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Workspaces.UITests;
using DisplayDeviceInfo = Microsoft.Workspaces.UITests.WorkspacesDisplay.DisplayDeviceInfo;
using TargetDisplay = Microsoft.Workspaces.UITests.WorkspacesDisplay.TargetDisplay;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

// Regression coverage for the product display-identity contract the Workspaces UI tests seed against.
// These exercise the pure resolution helpers only (no live topology), so they run deterministically on any
// host and CI, including where the host reports sparse/non-1 numbering, mixed DPI, or a numberless display.
[TestClass]
public sealed class WorkspacesDisplayTests
{
    private const uint Active = 0x1;
    private const uint Inactive = 0x0;
    private const uint Mirroring = 0x8;

    [TestMethod]
    public void ActiveDeviceYieldsNumberFromAdapterAndSplitDeviceId()
    {
        var devices = new[]
        {
            new DisplayDeviceInfo(
                "\\\\.\\DISPLAY3\\Monitor0",
                "\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
                Active),
        };

        var (number, id, instanceId) = WorkspacesDisplay.ResolveNumberAndId("\\\\.\\DISPLAY3", devices);

        Assert.AreEqual(3, number);
        Assert.AreEqual("GSM1388", id);
        Assert.AreEqual("4&125707d6&0&UID8388688", instanceId);
    }

    [TestMethod]
    public void FirstActiveNonMirroringDeviceIsSelected()
    {
        var devices = new[]
        {
            new DisplayDeviceInfo("\\\\.\\DISPLAY2\\Monitor0", "\\\\?\\DISPLAY#MIR#x#{g}", Active | Mirroring),
            new DisplayDeviceInfo("\\\\.\\DISPLAY2\\Monitor0", "\\\\?\\DISPLAY#OFF#x#{g}", Inactive),
            new DisplayDeviceInfo("\\\\.\\DISPLAY2\\Monitor0", "\\\\?\\DISPLAY#DELL0001#5&abcd&0&UID256#{g}", Active),
        };

        var (number, id, instanceId) = WorkspacesDisplay.ResolveNumberAndId("\\\\.\\DISPLAY2", devices);

        Assert.AreEqual(2, number);
        Assert.AreEqual("DELL0001", id);
        Assert.AreEqual("5&abcd&0&UID256", instanceId);
    }

    [TestMethod]
    public void NoActiveDeviceFallsBackToGdiDeviceName()
    {
        // Only inactive/mirroring devices -> product uses the GDI device name for both id and number.
        var devices = new[]
        {
            new DisplayDeviceInfo("\\\\.\\DISPLAY5\\Monitor0", "\\\\?\\DISPLAY#MIR#x#{g}", Mirroring),
        };

        var (number, id, instanceId) = WorkspacesDisplay.ResolveNumberAndId("\\\\.\\DISPLAY5", devices);

        Assert.AreEqual(5, number);
        Assert.AreEqual("\\\\.\\DISPLAY5", id);
        Assert.AreEqual(string.Empty, instanceId);
    }

    [TestMethod]
    public void NumberlessDisplayIsRejectedWithActionableGuidance()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => WorkspacesDisplay.ResolveNumberAndId("WinDisc", System.Array.Empty<DisplayDeviceInfo>()));

        StringAssert.Contains(exception.Message, "WinDisc");
        StringAssert.Contains(exception.Message, "Connect and unlock");
    }

    [TestMethod]
    public void ActiveDeviceWithNumberlessAdapterIsRejected()
    {
        var devices = new[]
        {
            new DisplayDeviceInfo("WinDisc", "\\\\?\\DISPLAY#RDP#x#{g}", Active),
        };

        Assert.ThrowsExactly<InvalidOperationException>(
            () => WorkspacesDisplay.ResolveNumberAndId("WinDisc", devices));
    }

    [TestMethod]
    public void SplitDisplayDeviceIdMatchesProductExample()
    {
        var (id, instanceId) = WorkspacesDisplay.SplitDisplayDeviceId(
            "\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");

        Assert.AreEqual("GSM1388", id);
        Assert.AreEqual("4&125707d6&0&UID8388688", instanceId);
    }

    [TestMethod]
    public void SplitDisplayDeviceIdReturnsWholeStringWhenMalformed()
    {
        var (id, instanceId) = WorkspacesDisplay.SplitDisplayDeviceId("no-delimiters");

        Assert.AreEqual("no-delimiters", id);
        Assert.AreEqual(string.Empty, instanceId);
    }

    [TestMethod]
    public void RemoveNonDigitsKeepsOnlyDigits()
    {
        Assert.AreEqual("12", WorkspacesDisplay.RemoveNonDigits("\\\\.\\DISPLAY12"));
        Assert.AreEqual(string.Empty, WorkspacesDisplay.RemoveNonDigits("WinDisc"));
    }

    [TestMethod]
    [DataRow(new[] { 3 }, 1)]
    [DataRow(new[] { 1 }, 2)]
    [DataRow(new[] { 1, 2 }, 3)]
    [DataRow(new[] { 2, 15 }, 1)]
    [DataRow(new[] { 1, 3, 11 }, 2)]
    [DataRow(new int[0], 1)]
    public void FirstUnusedNumberIsSmallestProvablyAbsent(int[] used, int expected)
    {
        Assert.AreEqual(expected, WorkspacesDisplay.FirstUnusedNumber(used));
    }

    [TestMethod]
    public void DefaultPositionKeepsBaselineOnTypicalWorkArea()
    {
        // 1920x1040 logical work area at origin (0,0), 96 DPI -> historical baseline is preserved.
        var target = MakeTarget(dpi: 96, monitorLeft: 0, monitorTop: 0, monitorRight: 1920, monitorBottom: 1080, workRight: 1920, workBottom: 1040);

        var (x, y, width, height) = WorkspacesDisplay.DefaultApplicationPosition(target);

        Assert.AreEqual(240, x);
        Assert.AreEqual(220, y);
        Assert.AreEqual(720, width);
        Assert.AreEqual(460, height);
    }

    [TestMethod]
    public void DefaultPositionScalesToLogicalWorkAreaAtHighDpi()
    {
        // 3840x2160 physical at 200% -> 1920x1080 logical; work area 1920x1040 logical -> baseline preserved.
        var target = MakeTarget(dpi: 192, monitorLeft: 0, monitorTop: 0, monitorRight: 3840, monitorBottom: 2160, workRight: 3840, workBottom: 2080);

        var (x, y, width, height) = WorkspacesDisplay.DefaultApplicationPosition(target);

        Assert.AreEqual(240, x);
        Assert.AreEqual(220, y);
        Assert.AreEqual(720, width);
        Assert.AreEqual(460, height);
    }

    [TestMethod]
    public void DefaultPositionFitsInsideSmallWorkArea()
    {
        // 1024x600 logical work area cannot hold the baseline -> derived rect stays inside with padding.
        var target = MakeTarget(dpi: 96, monitorLeft: 0, monitorTop: 0, monitorRight: 1024, monitorBottom: 600, workRight: 1024, workBottom: 600);

        var (x, y, width, height) = WorkspacesDisplay.DefaultApplicationPosition(target);

        Assert.IsTrue(x >= 0, $"x={x}");
        Assert.IsTrue(y >= 0, $"y={y}");
        Assert.IsTrue(width > 0 && height > 0, $"size={width}x{height}");
        Assert.IsTrue(x + width <= 1024, $"right={x + width}");
        Assert.IsTrue(y + height <= 600, $"bottom={y + height}");
    }

    [TestMethod]
    public void DefaultPositionRejectsMissingDpi()
    {
        var target = MakeTarget(dpi: 0, monitorLeft: 0, monitorTop: 0, monitorRight: 1920, monitorBottom: 1080, workRight: 1920, workBottom: 1040);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspacesDisplay.DefaultApplicationPosition(target));
    }

    [TestMethod]
    public void DefaultPositionRejectsEmptyWorkArea()
    {
        var target = MakeTarget(dpi: 96, monitorLeft: 0, monitorTop: 0, monitorRight: 1920, monitorBottom: 1080, workRight: 0, workBottom: 0);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspacesDisplay.DefaultApplicationPosition(target));
    }

    [TestMethod]
    public void LogicalWorkAreaHonorsDpiAndOffset()
    {
        // Primary at 150% with a left/top taskbar offset. Logical = physical * 96 / dpi.
        var target = MakeTarget(dpi: 144, monitorLeft: 0, monitorTop: 0, monitorRight: 2880, monitorBottom: 1620, workLeft: 96, workTop: 0, workRight: 2880, workBottom: 1560);

        Assert.AreEqual(64, target.LogicalWorkLeft);   // 96 * 96 / 144
        Assert.AreEqual(0, target.LogicalWorkTop);
        Assert.AreEqual(1920, target.LogicalWorkRight); // 2880 * 96 / 144
        Assert.AreEqual(1040, target.LogicalWorkBottom); // 1560 * 96 / 144
    }

    private static TargetDisplay MakeTarget(
        uint dpi,
        int monitorLeft,
        int monitorTop,
        int monitorRight,
        int monitorBottom,
        int workRight,
        int workBottom,
        int workLeft = 0,
        int workTop = 0) =>
        new(
            "\\\\.\\DISPLAY1",
            1,
            "GSM1388",
            "instance",
            dpi,
            monitorLeft,
            monitorTop,
            monitorRight,
            monitorBottom,
            workLeft,
            workTop,
            workRight,
            workBottom,
            true);
}

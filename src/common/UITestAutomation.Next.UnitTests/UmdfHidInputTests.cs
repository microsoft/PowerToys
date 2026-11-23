// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public class UmdfHidInputTests
{
    [TestMethod]
    public void EmptyKeyboardReportReleasesAllKeys()
    {
        CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 }, UmdfHidInput.CreateKeyboardReport(0, []));
    }

    [TestMethod]
    public void KeyboardReportPreservesModifiersAndDeduplicatesUsages()
    {
        CollectionAssert.AreEqual(
            new byte[] { 2, 0x0c, 0, 4, 5, 6, 0, 0, 0 },
            UmdfHidInput.CreateKeyboardReport(0x0c, [6, 4, 5, 4]));
    }

    [TestMethod]
    public void KeyboardReportAcceptsSixKeys()
    {
        CollectionAssert.AreEqual(
            new byte[] { 2, 0, 0, 4, 5, 6, 7, 8, 9 },
            UmdfHidInput.CreateKeyboardReport(0, [4, 5, 6, 7, 8, 9]));
    }

    [TestMethod]
    public void KeyboardReportRejectsRolloverRatherThanDroppingKeys()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => UmdfHidInput.CreateKeyboardReport(0, [4, 5, 6, 7, 8, 9, 10]));
    }

    [TestMethod]
    public void KeyboardReportRejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UmdfHidInput.CreateKeyboardReport(0, null!));
    }

    [TestMethod]
    [DataRow(Key.Ctrl, 1)]
    [DataRow(Key.LCtrl, 1)]
    [DataRow(Key.RCtrl, 16)]
    [DataRow(Key.Shift, 2)]
    [DataRow(Key.LShift, 2)]
    [DataRow(Key.Alt, 4)]
    [DataRow(Key.LWin, 8)]
    [DataRow(Key.A, 0)]
    public void ModifierMasksMatchKeyboardHidUsages(Key key, int expected)
    {
        Assert.AreEqual((byte)expected, UmdfHidInput.ModifierMask(key));
    }

    [TestMethod]
    [DataRow(0x1e, 0x04)]
    [DataRow(0x2c, 0x1d)]
    [DataRow(0x02, 0x1e)]
    [DataRow(0x0b, 0x27)]
    [DataRow(0x1c, 0x28)]
    [DataRow(0x01, 0x29)]
    [DataRow(0x34, 0x37)]
    [DataRow(0x3b, 0x3a)]
    [DataRow(0x58, 0x45)]
    [DataRow(0xe053, 0x4c)]
    [DataRow(0xe04f, 0x4d)]
    [DataRow(0xe04b, 0x50)]
    public void ScanCodesMapToPhysicalKeyboardUsages(int scanCode, int expected)
    {
        Assert.AreEqual((byte)expected, UmdfHidInput.ScanCodeToUsage((uint)scanCode));
    }

    [TestMethod]
    public void UnknownScanCodeDoesNotFallBackToInjectedInput()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => UmdfHidInput.ScanCodeToUsage(0xffff));
    }

    [TestMethod]
    [DataRow(Key.End, 0x4f, 0x4d)]
    [DataRow(Key.Delete, 0x53, 0x4c)]
    [DataRow(Key.Left, 0x4b, 0x50)]
    [DataRow(Key.Up, 0xe048, 0x52)]
    [DataRow(Key.A, 0x1e, 0x04)]
    public void NavigationKeysPreserveExtendedIdentityWhenMappingOmitsPrefix(Key key, int scanCode, int expected)
    {
        Assert.AreEqual((byte)expected, UmdfHidInput.KeyToUsage(key, (uint)scanCode));
    }

    [TestMethod]
    [DataRow(0, 1920, 9)]
    [DataRow(960, 1920, 16392)]
    [DataRow(1919, 1920, 32758)]
    public void AbsoluteCoordinatesTargetPixelCenters(int pixel, int extent, int expected)
    {
        Assert.AreEqual((ushort)expected, UmdfHidInput.Normalize(pixel, extent));
    }

    [TestMethod]
    [DataRow(-1, 1920)]
    [DataRow(1920, 1920)]
    [DataRow(0, 0)]
    public void InvalidCoordinatesAreNotSilentlyClamped(int pixel, int extent)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UmdfHidInput.Normalize(pixel, extent));
    }
}

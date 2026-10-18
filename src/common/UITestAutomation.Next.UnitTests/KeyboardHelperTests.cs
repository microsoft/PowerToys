// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public class KeyboardHelperTests
{
    [TestMethod]
    public void CreateChordPlanKeepsLeftControlSideSpecific()
    {
        var (chord, heldKeys) = KeyboardHelper.CreateChordPlan(Key.LCtrl, Key.A);

        Assert.AreEqual("a", chord);
        CollectionAssert.AreEqual(new[] { Key.LCtrl }, heldKeys.ToArray());
    }

    [TestMethod]
    public void CreateChordPlanKeepsRightControlSideSpecific()
    {
        var (chord, heldKeys) = KeyboardHelper.CreateChordPlan(Key.RCtrl, Key.A);

        Assert.AreEqual("a", chord);
        CollectionAssert.AreEqual(new[] { Key.RCtrl }, heldKeys.ToArray());
    }

    [TestMethod]
    public void SendKeysRejectsLayoutDependentOemKeys()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => KeyboardHelper.SendKeys(Key.OemPeriod));
    }

    [TestMethod]
    public void SendChordRejectsNullKeys()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => KeyboardHelper.SendChord(null!));
    }

    [TestMethod]
    public void SendChordRejectsEmptyKeys()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KeyboardHelper.SendChord());
    }

    [TestMethod]
    public void CreateChordInputPlanPressesInOrderAndReleasesInReverse()
    {
        var inputs = KeyboardHelper.CreateChordInputPlan(Key.LWin, Key.Ctrl, Key.Shift, Key.V);

        CollectionAssert.AreEqual(
            new ushort[] { 0x5B, 0x11, 0x10, 0x56, 0x56, 0x10, 0x11, 0x5B },
            inputs.Select(input => input.Data.Keyboard.VirtualKey).ToArray());
        CollectionAssert.AreEqual(
            new uint[] { 1, 0, 0, 0, 2, 2, 2, 3 },
            inputs.Select(input => input.Data.Keyboard.Flags).ToArray());
        foreach (var input in inputs)
        {
            Assert.AreEqual(1u, input.Type);
            Assert.AreEqual<ushort>(0, input.Data.Keyboard.ScanCode);
            Assert.AreEqual(0u, input.Data.Keyboard.Time);
            Assert.AreEqual(UIntPtr.Zero, input.Data.Keyboard.ExtraInfo);
        }
    }

    [TestMethod]
    [DataRow(Key.LWin, 1u)]
    [DataRow(Key.RCtrl, 1u)]
    [DataRow(Key.Left, 1u)]
    [DataRow(Key.Up, 1u)]
    [DataRow(Key.Right, 1u)]
    [DataRow(Key.Down, 1u)]
    [DataRow(Key.Home, 1u)]
    [DataRow(Key.End, 1u)]
    [DataRow(Key.PageUp, 1u)]
    [DataRow(Key.PageDown, 1u)]
    [DataRow(Key.Insert, 1u)]
    [DataRow(Key.Delete, 1u)]
    [DataRow(Key.Ctrl, 0u)]
    [DataRow(Key.LCtrl, 0u)]
    [DataRow(Key.Shift, 0u)]
    [DataRow(Key.LShift, 0u)]
    [DataRow(Key.Alt, 0u)]
    [DataRow(Key.A, 0u)]
    [DataRow(Key.Z, 0u)]
    public void CreateChordInputPlanSetsExtendedAndKeyUpFlags(Key key, uint expectedKeyDownFlags)
    {
        var inputs = KeyboardHelper.CreateChordInputPlan(key);

        Assert.AreEqual(2, inputs.Length);
        Assert.AreEqual(expectedKeyDownFlags, inputs[0].Data.Keyboard.Flags);
        Assert.AreEqual(expectedKeyDownFlags | 2u, inputs[1].Data.Keyboard.Flags);
    }

    [TestMethod]
    public void InputLayoutMatchesNative64BitLayout()
    {
        Assert.AreEqual(8, IntPtr.Size, "The test project targets x64 and ARM64.");
        Assert.AreEqual(40, Marshal.SizeOf<KeyboardHelper.Input>());
        Assert.AreEqual(32, Marshal.SizeOf<KeyboardHelper.InputData>());
        Assert.AreEqual(24, Marshal.SizeOf<KeyboardHelper.KeyboardInput>());
        Assert.AreEqual(32, Marshal.SizeOf<KeyboardHelper.MouseInput>());
        Assert.AreEqual(0, Marshal.OffsetOf<KeyboardHelper.Input>(nameof(KeyboardHelper.Input.Type)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<KeyboardHelper.Input>(nameof(KeyboardHelper.Input.Data)).ToInt32());
        Assert.AreEqual(0, Marshal.OffsetOf<KeyboardHelper.InputData>(nameof(KeyboardHelper.InputData.Keyboard)).ToInt32());
        Assert.AreEqual(0, Marshal.OffsetOf<KeyboardHelper.InputData>(nameof(KeyboardHelper.InputData.Mouse)).ToInt32());
        Assert.AreEqual(16, Marshal.OffsetOf<KeyboardHelper.KeyboardInput>(nameof(KeyboardHelper.KeyboardInput.ExtraInfo)).ToInt32());
        Assert.AreEqual(24, Marshal.OffsetOf<KeyboardHelper.MouseInput>(nameof(KeyboardHelper.MouseInput.ExtraInfo)).ToInt32());
    }
}

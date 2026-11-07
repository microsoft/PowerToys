// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class PasteAndTypingTests
{
    [TestMethod]
    public void WaitForRead_AppReadsTheClipboard_HandsItBackAfterTheFloor()
    {
        var clock = new FakeClock();
        var open = false;
        clock.Tick = now => open = now is >= 40 and < 45;

        Assert.IsTrue(PasteSettle.WaitForRead(() => open, () => true, clock, sentAt: 0));
        Assert.IsTrue(clock.ElapsedMilliseconds >= PasteSettle.FloorMs + PasteSettle.GraceMs, $"Handed back at {clock.ElapsedMilliseconds} ms.");
        Assert.IsTrue(clock.ElapsedMilliseconds < PasteSettle.SettleMs, $"Handed back at {clock.ElapsedMilliseconds} ms.");
    }

    [TestMethod]
    public void WaitForRead_ReadNeverSeen_HandsItBackAtTheSettle()
    {
        var clock = new FakeClock();

        Assert.IsTrue(PasteSettle.WaitForRead(() => false, () => true, clock, sentAt: 0));
        Assert.IsTrue(clock.ElapsedMilliseconds is >= PasteSettle.SettleMs and < PasteSettle.SettleMs + 20, $"Handed back at {clock.ElapsedMilliseconds} ms.");
    }

    [TestMethod]
    public void WaitForRead_BusyApp_WaitsUntilItAnswersAndGivesItTheFloorAgain()
    {
        var clock = new FakeClock();

        Assert.IsTrue(PasteSettle.WaitForRead(() => false, () => clock.ElapsedMilliseconds >= 1000, clock, sentAt: 0));
        Assert.IsTrue(clock.ElapsedMilliseconds >= 1000 + PasteSettle.FloorMs, $"Handed back at {clock.ElapsedMilliseconds} ms.");
    }

    [TestMethod]
    public void WaitForRead_AppBusyPastTheCap_KeepsTheSnippetOnTheClipboard()
    {
        var clock = new FakeClock();

        Assert.IsFalse(PasteSettle.WaitForRead(() => false, () => false, clock, sentAt: 0));
        Assert.IsTrue(clock.ElapsedMilliseconds >= PasteSettle.BusyCapMs);
    }

    [TestMethod]
    public void Paste_HeldModifiers_AreLetGoOfBeforeCtrlV()
        => CollectionAssert.AreEqual(
            new[]
            {
                KeyStroke.KeyUp(KeyStrokes.LeftShift),
                KeyStroke.KeyDown(KeyStrokes.Control),
                KeyStroke.KeyDown(KeyStrokes.V),
                KeyStroke.KeyUp(KeyStrokes.V),
                KeyStroke.KeyUp(KeyStrokes.Control),
            },
            KeyStrokes.Paste([KeyStrokes.LeftShift]).ToArray());

    [TestMethod]
    [DataRow(KeyStrokes.LeftAlt)]
    [DataRow(KeyStrokes.RightWindows)]
    public void Paste_AltOrWindowsHeld_GetsAKeyThatDoesNothingBeforeItsLetGoOf(ushort modifier)
        => CollectionAssert.AreEqual(
            new[]
            {
                KeyStroke.KeyDown(KeyStrokes.Dummy),
                KeyStroke.KeyUp(KeyStrokes.Dummy),
                KeyStroke.KeyUp(modifier),
                KeyStroke.KeyDown(KeyStrokes.Control),
                KeyStroke.KeyDown(KeyStrokes.V),
                KeyStroke.KeyUp(KeyStrokes.V),
                KeyStroke.KeyUp(KeyStrokes.Control),
            },
            KeyStrokes.Paste([modifier]).ToArray());

    [TestMethod]
    public void AfterReleasing_AltHeld_GetsAKeyThatDoesNothingFirst()
        => CollectionAssert.AreEqual(
            new[]
            {
                KeyStroke.KeyDown(KeyStrokes.Dummy),
                KeyStroke.KeyUp(KeyStrokes.Dummy),
                KeyStroke.KeyUp(KeyStrokes.LeftAlt),
                KeyStroke.KeyDown(KeyStrokes.Tab),
                KeyStroke.KeyUp(KeyStrokes.Tab),
            },
            KeyStrokes.AfterReleasing([KeyStrokes.LeftAlt], [KeyStroke.KeyDown(KeyStrokes.Tab), KeyStroke.KeyUp(KeyStrokes.Tab)]).ToArray());

    [TestMethod]
    public void InputTag_HasKeyboardManagersBitInAValueOfItsOwn()
    {
        // 0x110 lacked Keyboard Manager's bit, so with Caps Lock and Ctrl swapped, the extension's Ctrl+V was remapped.
        Assert.AreEqual((nuint)1, Win32Desktop.InputTag & 1, "Keyboard Manager leaves keys with its bit alone.");
        Assert.AreEqual((nuint)InputTags.CommandPalette, Win32Desktop.InputTag, "Text Expander's hook never matches this value.");
        foreach (nuint taken in new nuint[] { 0x11, 0x101, 0x110, 0x111 })
        {
            Assert.AreNotEqual(taken, Win32Desktop.InputTag, "Keyboard Manager's and the runner's own tags mean something else.");
        }
    }

    [TestMethod]
    public void IsExtended_RightHandCtrlAndAltAndTheWindowsKeys_AreSentAsExtended()
    {
        // Let go of without the flag, as Text Expander never sends them, the app saw the left-hand key go up.
        foreach (var key in KeyStrokes.Modifiers)
        {
            var extended = key is KeyStrokes.RightControl or KeyStrokes.RightAlt or KeyStrokes.LeftWindows or KeyStrokes.RightWindows;
            Assert.AreEqual(extended, KeyStrokes.IsExtended(key), $"0x{key:X2}");
        }

        Assert.IsFalse(KeyStrokes.IsExtended(KeyStrokes.Control));
        Assert.IsFalse(KeyStrokes.IsExtended(KeyStrokes.Return));
    }

    [TestMethod]
    public void Typing_LineBreaksAndTabs_AreKeysAndCarriageReturnsAreSkipped()
    {
        var typed = KeyStrokes.Typing("a\r\n\tb").ToArray();

        Assert.AreEqual(4, typed.Length);
        CollectionAssert.AreEqual(new[] { KeyStroke.CharacterDown('a'), KeyStroke.CharacterUp('a') }, typed[0].Strokes.ToArray());
        CollectionAssert.AreEqual(new[] { KeyStroke.KeyDown(KeyStrokes.Return), KeyStroke.KeyUp(KeyStrokes.Return) }, typed[1].Strokes.ToArray());
        Assert.IsTrue(typed[1].IsKey);
        Assert.IsTrue(typed[1].PausesLonger);
        CollectionAssert.AreEqual(new[] { KeyStroke.KeyDown(KeyStrokes.Tab), KeyStroke.KeyUp(KeyStrokes.Tab) }, typed[2].Strokes.ToArray());
        Assert.IsFalse(typed[2].PausesLonger);
    }

    [TestMethod]
    public void Typing_SurrogatePair_GoesAsOneBatch()
    {
        var typed = KeyStrokes.Typing("\U0001F600.").ToArray();

        Assert.AreEqual(2, typed.Length);
        CollectionAssert.AreEqual(
            new[] { KeyStroke.CharacterDown('\uD83D'), KeyStroke.CharacterUp('\uD83D'), KeyStroke.CharacterDown('\uDE00'), KeyStroke.CharacterUp('\uDE00') },
            typed[0].Strokes.ToArray());
        Assert.IsTrue(typed[1].PausesLonger);
    }
}

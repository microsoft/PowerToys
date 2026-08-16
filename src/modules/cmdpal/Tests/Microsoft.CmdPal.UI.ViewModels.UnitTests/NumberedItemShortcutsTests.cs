// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NumberedItemShortcutsTests
{
    private static readonly int[] ExpectedTargetIndexes = [1, 2, 4, 5, 6, 7, 8, 9, 10];

    [DataTestMethod]
    [DataRow((int)VirtualKey.Number1, false, true, false, false, (int)NumberedItemShortcuts.ShortcutAction.Invoke, 0, (int)NumberedItemShortcuts.ShortcutAction.Invoke)]
    [DataRow((int)VirtualKey.Number1, false, true, false, false, (int)NumberedItemShortcuts.ShortcutAction.Select, 0, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    [DataRow((int)VirtualKey.Number9, false, true, true, false, (int)NumberedItemShortcuts.ShortcutAction.Invoke, 8, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    [DataRow((int)VirtualKey.Number9, false, true, true, false, (int)NumberedItemShortcuts.ShortcutAction.Select, 8, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    public void Resolve_MapsConfiguredActionAndShiftSelection(
        int key,
        bool ctrl,
        bool alt,
        bool shift,
        bool win,
        int plainAltAction,
        int expectedIndex,
        int expectedAction)
    {
        var shortcut = NumberedItemShortcuts.Resolve(
            (VirtualKey)key,
            ctrl,
            alt,
            shift,
            win,
            (NumberedItemShortcuts.ShortcutAction)plainAltAction);

        Assert.IsNotNull(shortcut);
        Assert.AreEqual(expectedIndex, shortcut.Value.Index);
        Assert.AreEqual((NumberedItemShortcuts.ShortcutAction)expectedAction, shortcut.Value.Action);
    }

    [DataTestMethod]
    [DataRow((int)VirtualKey.Number1, true, true, false, false)]
    [DataRow((int)VirtualKey.Number1, false, false, false, false)]
    [DataRow((int)VirtualKey.Number1, false, true, false, true)]
    [DataRow((int)VirtualKey.Number0, false, true, false, false)]
    [DataRow((int)VirtualKey.NumberPad1, false, true, false, false)]
    public void Resolve_RejectsOtherChords(int key, bool ctrl, bool alt, bool shift, bool win)
    {
        Assert.IsNull(NumberedItemShortcuts.Resolve(
            (VirtualKey)key,
            ctrl,
            alt,
            shift,
            win,
            NumberedItemShortcuts.ShortcutAction.Invoke));
    }

    [DataTestMethod]
    [DataRow(false, (int)NumberedItemShortcuts.ShortcutAction.Invoke)]
    [DataRow(true, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    public void Resolve_MapsLatchedAccessKeySequence(bool shift, int expectedAction)
    {
        var shortcut = NumberedItemShortcuts.Resolve(
            VirtualKey.Number3,
            ctrl: false,
            alt: false,
            shift,
            win: false,
            NumberedItemShortcuts.ShortcutAction.Invoke,
            isAccessKeyModeActive: true);

        Assert.IsNotNull(shortcut);
        Assert.AreEqual(2, shortcut.Value.Index);
        Assert.AreEqual((NumberedItemShortcuts.ShortcutAction)expectedAction, shortcut.Value.Action);
    }

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Resolve_RejectsModifiedLatchedAccessKeySequence(bool ctrl, bool win)
    {
        Assert.IsNull(NumberedItemShortcuts.Resolve(
            VirtualKey.Number3,
            ctrl,
            alt: false,
            shift: false,
            win,
            NumberedItemShortcuts.ShortcutAction.Invoke,
            isAccessKeyModeActive: true));
    }

    [TestMethod]
    public void GetTargets_SkipsIneligibleItemsAndCapsTheResult()
    {
        var items = Enumerable.Range(0, 12)
            .Select(index => new TestItem(index, index is not 0 and not 3))
            .ToArray();

        var targets = NumberedItemShortcuts.GetTargets(items, static item => item.IsEligible);

        CollectionAssert.AreEqual(
            ExpectedTargetIndexes,
            targets.Select(item => item.Index).ToArray());
    }

    [DataTestMethod]
    [DataRow(-1, -1)]
    [DataRow(0, -1)]
    [DataRow(1, 0)]
    [DataRow(3, -1)]
    [DataRow(4, 2)]
    [DataRow(10, 8)]
    [DataRow(11, -1)]
    [DataRow(12, -1)]
    public void GetShortcutIndex_UsesProjectedPositionAndSkipsIneligibleItems(int itemIndex, int expectedShortcutIndex)
    {
        var items = Enumerable.Range(0, 12)
            .Select(index => new TestItem(index, index is not 0 and not 3))
            .ToArray();

        Assert.AreEqual(
            expectedShortcutIndex,
            NumberedItemShortcuts.GetShortcutIndex(items, itemIndex, static item => item.IsEligible));
    }

    private sealed record TestItem(int Index, bool IsEligible);
}

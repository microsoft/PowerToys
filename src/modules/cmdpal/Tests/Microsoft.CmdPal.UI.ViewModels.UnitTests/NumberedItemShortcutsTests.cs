// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NumberedItemShortcutsTests
{
    private static readonly int[] ExpectedTargetIndexes = [1, 2, 4, 5, 6, 7, 8, 9, 10];

    [DataTestMethod]
    [DataRow((int)VirtualKey.Number1, (int)VirtualKeyModifiers.Menu, (int)NumberedItemShortcuts.ShortcutAction.Invoke, 0, (int)NumberedItemShortcuts.ShortcutAction.Invoke)]
    [DataRow((int)VirtualKey.Number1, (int)VirtualKeyModifiers.Menu, (int)NumberedItemShortcuts.ShortcutAction.Select, 0, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    [DataRow((int)VirtualKey.Number9, (int)(VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift), (int)NumberedItemShortcuts.ShortcutAction.Invoke, 8, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    [DataRow((int)VirtualKey.Number9, (int)(VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift), (int)NumberedItemShortcuts.ShortcutAction.Select, 8, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    public void Resolve_MapsConfiguredActionAndShiftSelection(
        int key,
        int modifiers,
        int plainAltAction,
        int expectedIndex,
        int expectedAction)
    {
        var shortcut = NumberedItemShortcuts.Resolve(
            Chord((VirtualKey)key, (VirtualKeyModifiers)modifiers),
            (NumberedItemShortcuts.ShortcutAction)plainAltAction,
            isAccessKeyModeActive: false);

        Assert.IsNotNull(shortcut);
        Assert.AreEqual(expectedIndex, shortcut.Value.Index);
        Assert.AreEqual((NumberedItemShortcuts.ShortcutAction)expectedAction, shortcut.Value.Action);
    }

    [DataTestMethod]
    [DataRow((int)VirtualKey.Number1, (int)(VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu))]
    [DataRow((int)VirtualKey.Number1, (int)VirtualKeyModifiers.None)]
    [DataRow((int)VirtualKey.Number1, (int)(VirtualKeyModifiers.Menu | VirtualKeyModifiers.Windows))]
    [DataRow((int)VirtualKey.Number0, (int)VirtualKeyModifiers.Menu)]
    [DataRow((int)VirtualKey.NumberPad1, (int)VirtualKeyModifiers.Menu)]
    public void Resolve_RejectsOtherChords(int key, int modifiers)
    {
        Assert.IsNull(NumberedItemShortcuts.Resolve(
            Chord((VirtualKey)key, (VirtualKeyModifiers)modifiers),
            NumberedItemShortcuts.ShortcutAction.Invoke,
            isAccessKeyModeActive: false));
    }

    [DataTestMethod]
    [DataRow((int)VirtualKeyModifiers.None, (int)NumberedItemShortcuts.ShortcutAction.Invoke)]
    [DataRow((int)VirtualKeyModifiers.Shift, (int)NumberedItemShortcuts.ShortcutAction.Select)]
    public void Resolve_MapsLatchedAccessKeySequence(int modifiers, int expectedAction)
    {
        var shortcut = NumberedItemShortcuts.Resolve(
            Chord(VirtualKey.Number3, (VirtualKeyModifiers)modifiers),
            NumberedItemShortcuts.ShortcutAction.Invoke,
            isAccessKeyModeActive: true);

        Assert.IsNotNull(shortcut);
        Assert.AreEqual(2, shortcut.Value.Index);
        Assert.AreEqual((NumberedItemShortcuts.ShortcutAction)expectedAction, shortcut.Value.Action);
    }

    [DataTestMethod]
    [DataRow((int)VirtualKeyModifiers.Control)]
    [DataRow((int)VirtualKeyModifiers.Windows)]
    public void Resolve_RejectsModifiedLatchedAccessKeySequence(int modifiers)
    {
        Assert.IsNull(NumberedItemShortcuts.Resolve(
            Chord(VirtualKey.Number3, (VirtualKeyModifiers)modifiers),
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

    private static KeyChord Chord(VirtualKey key, VirtualKeyModifiers modifiers) =>
        new(modifiers, (int)key, 0);

    private sealed record TestItem(int Index, bool IsEligible);
}

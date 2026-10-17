// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class DockFocusNavigationTests
{
    [TestMethod]
    [DataRow(3, true, false, "start", 1)]
    [DataRow(3, true, true, "start", 1)]
    [DataRow(3, false, false, "next", 0)]
    [DataRow(3, false, true, "next", 2)]
    [DataRow(0, false, false, "next", 0)]
    [DataRow(0, false, true, "next", 2)]
    public void TryFocusAcrossDocks_RestoresOnlyStartingDock(int startingItemCount, bool startingDockCanFocus, bool reverse, string expectedDock, int expectedIndex)
    {
        string[] dockOrder = ["start", "next", "last"];
        string? focusedDock = null;
        var focusedIndex = -1;

        var result = DockFocusNavigation.TryFocusAcrossDocks(
            dockOrder,
            "START",
            moveFromCurrent: false,
            restoreLastFocus: true,
            (dockId, _, restore) => DockFocusNavigation.TryFocusNext(
                dockId == "start" ? startingItemCount : 3,
                -1,
                index =>
                {
                    if (dockId == "start" && !startingDockCanFocus)
                    {
                        return false;
                    }

                    focusedDock = dockId;
                    focusedIndex = index;
                    return true;
                },
                wrap: false,
                reverse: reverse,
                rememberedIndex: restore ? 1 : -1));

        Assert.IsTrue(result);
        Assert.AreEqual(expectedDock, focusedDock);
        Assert.AreEqual(expectedIndex, focusedIndex);
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 2)]
    public void TryFocusAcrossDocks_LeavingFocusedDock_EntersNextDockFromDirectionalEdge(bool reverse, int expectedIndex)
    {
        string[] dockOrder = ["start", "next", "last"];
        string? focusedDock = null;
        var focusedIndex = -1;

        var result = DockFocusNavigation.TryFocusAcrossDocks(
            dockOrder,
            "start",
            moveFromCurrent: true,
            restoreLastFocus: false,
            (dockId, move, restore) => DockFocusNavigation.TryFocusNext(
                3,
                move ? reverse ? 0 : 2 : -1,
                index =>
                {
                    focusedDock = dockId;
                    focusedIndex = index;
                    return true;
                },
                wrap: false,
                reverse: reverse,
                rememberedIndex: restore ? 1 : -1));

        Assert.IsTrue(result);
        Assert.AreEqual("next", focusedDock);
        Assert.AreEqual(expectedIndex, focusedIndex);
    }

    [TestMethod]
    [DataRow(false, 2, 0)]
    [DataRow(true, 0, 2)]
    public void TryFocusAcrossDocks_PassingBoundary_ResetsDepartedDockForNextVisit(bool reverse, int startingIndex, int expectedIndex)
    {
        string[] dockOrder = ["start", "next", "last"];
        Dictionary<string, int> rememberedIndices = new()
        {
            ["start"] = startingIndex,
            ["next"] = 1,
            ["last"] = 1,
        };
        var focusedDock = "start";
        var focusedIndex = startingIndex;

        bool TryFocus(string dockId, bool move, bool restore) => DockFocusNavigation.TryFocusNext(
            3,
            move ? focusedIndex : -1,
            index =>
            {
                focusedDock = dockId;
                focusedIndex = index;
                rememberedIndices[dockId] = index;
                return true;
            },
            wrap: false,
            reverse: reverse,
            rememberedIndex: restore ? rememberedIndices[dockId] : -1);

        void ResetFocus(string dockId) => rememberedIndices[dockId] = -1;

        Assert.IsTrue(DockFocusNavigation.TryFocusAcrossDocks(dockOrder, "start", moveFromCurrent: true, restoreLastFocus: false, TryFocus, ResetFocus));
        Assert.AreEqual("next", focusedDock);
        Assert.AreEqual(-1, rememberedIndices["start"]);
        Assert.AreEqual(expectedIndex, rememberedIndices["next"]);
        Assert.AreEqual(1, rememberedIndices["last"]);

        // Start a fresh cycle on the dock whose boundary was crossed.
        Assert.IsTrue(DockFocusNavigation.TryFocusAcrossDocks(dockOrder, "start", moveFromCurrent: false, restoreLastFocus: true, TryFocus, ResetFocus));
        Assert.AreEqual("start", focusedDock);
        Assert.AreEqual(expectedIndex, focusedIndex);
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 0)]
    public void TryFocusAcrossDocks_WithinBoundary_PreservesFocusForNextVisit(bool reverse, int expectedIndex)
    {
        string[] dockOrder = ["start", "next"];
        var focusedIndex = 1;
        var rememberedIndex = 1;

        bool TryFocus(string dockId, bool move, bool restore)
        {
            Assert.AreEqual("start", dockId);
            return DockFocusNavigation.TryFocusNext(
                3,
                move ? focusedIndex : -1,
                index =>
                {
                    focusedIndex = index;
                    rememberedIndex = index;
                    return true;
                },
                wrap: false,
                reverse: reverse,
                rememberedIndex: restore ? rememberedIndex : -1);
        }

        void ResetFocus(string dockId) => Assert.Fail($"Dock {dockId} must keep its focus until traversal reaches a boundary.");

        Assert.IsTrue(DockFocusNavigation.TryFocusAcrossDocks(dockOrder, "start", moveFromCurrent: true, restoreLastFocus: false, TryFocus, ResetFocus));
        Assert.AreEqual(expectedIndex, rememberedIndex);

        // Leaving for another app keeps the item available for restoration.
        Assert.IsTrue(DockFocusNavigation.TryFocusAcrossDocks(dockOrder, "start", moveFromCurrent: false, restoreLastFocus: true, TryFocus, ResetFocus));
        Assert.AreEqual(expectedIndex, focusedIndex);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 0)]
    [DataRow(3, 0)]
    public void TryFocusNext_AdvancesFromCurrentItemAndWraps(int focusedIndex, int expectedIndex)
    {
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(3, focusedIndex, index =>
        {
            attempts.Add(index);
            return true;
        });

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { expectedIndex }, attempts);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void TryFocusNext_EmptyDock_DoesNotAttemptFocus(bool wrap, bool reverse)
    {
        var result = DockFocusNavigation.TryFocusNext(
            0,
            -1,
            _ =>
            {
                Assert.Fail("An empty dock must not attempt to focus an item.");
                return false;
            },
            wrap,
            reverse,
            rememberedIndex: 0);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryFocusNext_SingleItem_KeepsFocus(bool reverse)
    {
        int[] expectedAttempts = [0];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            1,
            0,
            index =>
            {
                attempts.Add(index);
                return true;
            },
            reverse: reverse);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    public void TryFocusNext_SkipsUnfocusableItemsAcrossWrap()
    {
        int[] expectedAttempts = [2, 3, 0];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(4, 1, index =>
        {
            attempts.Add(index);
            return index == 0;
        });

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    public void TryFocusNext_NoFocusableItems_AttemptsEachItemOnce()
    {
        int[] expectedAttempts = [2, 0, 1];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(3, 1, index =>
        {
            attempts.Add(index);
            return false;
        });

        Assert.IsFalse(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(3, 2)]
    public void TryFocusNext_WithoutWrappingAtLastItem_LeavesFocusForNextDock(int itemCount, int focusedIndex)
    {
        var result = DockFocusNavigation.TryFocusNext(
            itemCount,
            focusedIndex,
            _ =>
            {
                Assert.Fail("The last item must hand traversal to the next dock without wrapping.");
                return false;
            },
            wrap: false);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryFocusNext_WithoutWrapping_SkipsUnfocusableItemsAndStopsAtEnd()
    {
        int[] expectedAttempts = [2, 3];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            4,
            1,
            index =>
            {
                attempts.Add(index);
                return false;
            },
            wrap: false);

        Assert.IsFalse(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    public void TryFocusNext_EnteringDockWithoutWrapping_StartsAtFirstFocusableItem()
    {
        int[] expectedAttempts = [0, 1];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            -1,
            index =>
            {
                attempts.Add(index);
                return index == 1;
            },
            wrap: false);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    [DataRow(-1, 2)]
    [DataRow(0, 2)]
    [DataRow(1, 0)]
    [DataRow(2, 1)]
    [DataRow(3, 2)]
    public void TryFocusNext_Reverse_MovesBackFromCurrentItemAndWraps(int focusedIndex, int expectedIndex)
    {
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            focusedIndex,
            index =>
            {
                attempts.Add(index);
                return true;
            },
            reverse: true);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { expectedIndex }, attempts);
    }

    [TestMethod]
    public void TryFocusNext_Reverse_SkipsUnfocusableItemsAcrossWrap()
    {
        int[] expectedAttempts = [0, 3, 2];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            4,
            1,
            index =>
            {
                attempts.Add(index);
                return index == 2;
            },
            reverse: true);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    public void TryFocusNext_Reverse_NoFocusableItems_AttemptsEachItemOnce()
    {
        int[] expectedAttempts = [0, 2, 1];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            1,
            index =>
            {
                attempts.Add(index);
                return false;
            },
            reverse: true);

        Assert.IsFalse(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public void TryFocusNext_Reverse_WithoutWrappingAtFirstItem_LeavesFocusForPreviousDock(int itemCount)
    {
        var result = DockFocusNavigation.TryFocusNext(
            itemCount,
            0,
            _ =>
            {
                Assert.Fail("The first item must hand traversal to the previous dock without wrapping.");
                return false;
            },
            wrap: false,
            reverse: true);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryFocusNext_Reverse_WithoutWrapping_SkipsUnfocusableItemsAndStopsAtStart()
    {
        int[] expectedAttempts = [1, 0];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            4,
            2,
            index =>
            {
                attempts.Add(index);
                return false;
            },
            wrap: false,
            reverse: true);

        Assert.IsFalse(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    public void TryFocusNext_Reverse_EnteringDock_StartsAtLastFocusableItem()
    {
        int[] expectedAttempts = [2, 1];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            -1,
            index =>
            {
                attempts.Add(index);
                return index == 1;
            },
            wrap: false,
            reverse: true);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    public void TryFocusNext_EnteringDock_RestoresRememberedItem(int rememberedIndex, bool reverse)
    {
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            -1,
            index =>
            {
                attempts.Add(index);
                return true;
            },
            wrap: false,
            reverse: reverse,
            rememberedIndex: rememberedIndex);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { rememberedIndex }, attempts);
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 0)]
    public void TryFocusNext_AlreadyFocused_AdvancesInsteadOfRestoring(bool reverse, int expectedIndex)
    {
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            1,
            index =>
            {
                attempts.Add(index);
                return true;
            },
            reverse: reverse,
            rememberedIndex: 1);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { expectedIndex }, attempts);
    }

    [TestMethod]
    [DataRow(-1, false, 0)]
    [DataRow(3, false, 0)]
    [DataRow(-1, true, 2)]
    [DataRow(3, true, 2)]
    public void TryFocusNext_MissingRememberedItem_StartsAtDirectionalEdge(int rememberedIndex, bool reverse, int expectedIndex)
    {
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            -1,
            index =>
            {
                attempts.Add(index);
                return true;
            },
            wrap: false,
            reverse: reverse,
            rememberedIndex: rememberedIndex);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { expectedIndex }, attempts);
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 2)]
    public void TryFocusNext_UnfocusableRememberedItem_FallsBackToDirectionalEdge(bool reverse, int expectedIndex)
    {
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            -1,
            index =>
            {
                attempts.Add(index);
                return index == expectedIndex;
            },
            wrap: false,
            reverse: reverse,
            rememberedIndex: 1);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { 1, expectedIndex }, attempts);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void TryFocusNext_NoFocusableItemsWithRememberedItem_AttemptsEachItemOnce(bool wrap, bool reverse)
    {
        int[] expectedAttempts = reverse ? [1, 2, 0] : [1, 0, 2];
        List<int> attempts = [];

        var result = DockFocusNavigation.TryFocusNext(
            3,
            -1,
            index =>
            {
                attempts.Add(index);
                return false;
            },
            wrap: wrap,
            reverse: reverse,
            rememberedIndex: 1);

        Assert.IsFalse(result);
        CollectionAssert.AreEqual(expectedAttempts, attempts);
    }

    [TestMethod]
    [DataRow(VirtualKey.J, VirtualKeyModifiers.Windows | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift, true)]
    [DataRow(VirtualKey.J, VirtualKeyModifiers.Windows | VirtualKeyModifiers.Menu, false)]
    [DataRow(VirtualKey.J, VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift, false)]
    [DataRow(VirtualKey.J, VirtualKeyModifiers.Windows | VirtualKeyModifiers.Shift, false)]
    [DataRow(VirtualKey.J, VirtualKeyModifiers.Windows | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift | VirtualKeyModifiers.Control, false)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Windows | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift, false)]
    public void IsReverseShortcut_DefaultShortcut_RequiresExactModifiersAndKey(VirtualKey key, VirtualKeyModifiers modifiers, bool expected)
    {
        var chord = new KeyChord(modifiers, (int)key, 0);

        Assert.AreEqual(expected, DockFocusNavigation.IsReverseShortcut(SettingsModel.DefaultDockFocusShortcut, chord));
    }

    [TestMethod]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, true)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, false)]
    [DataRow(VirtualKey.J, VirtualKeyModifiers.Windows | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift, false)]
    public void IsReverseShortcut_UsesConfiguredShortcut(VirtualKey key, VirtualKeyModifiers modifiers, bool expected)
    {
        var shortcut = new HotkeySettings(false, true, false, false, (int)VirtualKey.K);
        var chord = new KeyChord(modifiers, (int)key, 0);

        Assert.AreEqual(expected, DockFocusNavigation.IsReverseShortcut(shortcut, chord));
    }

    [TestMethod]
    [DataRow(VirtualKeyModifiers.Control)]
    [DataRow(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    public void IsReverseShortcut_ForwardShortcutIncludesShift_IsUnavailable(VirtualKeyModifiers modifiers)
    {
        var shortcut = new HotkeySettings(false, true, false, true, (int)VirtualKey.K);
        var chord = new KeyChord(modifiers, (int)VirtualKey.K, 0);

        Assert.IsFalse(DockFocusNavigation.IsReverseShortcut(shortcut, chord));
    }

    [TestMethod]
    public void IsReverseShortcut_UnsetShortcut_IsUnavailable()
    {
        var chord = new KeyChord(VirtualKeyModifiers.Shift, (int)VirtualKey.None, 0);

        Assert.IsFalse(DockFocusNavigation.IsReverseShortcut(null, chord));
        Assert.IsFalse(DockFocusNavigation.IsReverseShortcut(new HotkeySettings(), chord));
    }
}

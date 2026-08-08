// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class RemappingHelperTests
    {
        // Test that modifier keys pressed in reverse order (Shift, Alt, Ctrl, Win)
        // are sorted into the standard display order (Win, Ctrl, Alt, Shift)
        [TestMethod]
        public void SortModifierKeys_ShouldSortInCanonicalOrder_WhenPressedInReverseOrder()
        {
            // Arrange - keys in reverse of standard order
            var keys = new List<VirtualKey>
            {
                VirtualKey.Shift,
                VirtualKey.Menu,        // Alt
                VirtualKey.Control,
                VirtualKey.LeftWindows,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - should be Win, Ctrl, Alt, Shift
            Assert.AreEqual(VirtualKey.LeftWindows, keys[0]);
            Assert.AreEqual(VirtualKey.Control, keys[1]);
            Assert.AreEqual(VirtualKey.Menu, keys[2]);
            Assert.AreEqual(VirtualKey.Shift, keys[3]);
        }

        // Test the specific bug scenario: Win+Shift+S where Shift was pressed before Win
        [TestMethod]
        public void SortModifierKeys_ShouldShowWinBeforeShift_WhenShiftPressedFirst()
        {
            // Arrange - Shift pressed before Win (the reported bug scenario)
            var keys = new List<VirtualKey>
            {
                VirtualKey.Shift,
                VirtualKey.LeftWindows,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - Win should come before Shift
            Assert.AreEqual(VirtualKey.LeftWindows, keys[0]);
            Assert.AreEqual(VirtualKey.Shift, keys[1]);
        }

        // Test that keys already in the correct order remain unchanged
        [TestMethod]
        public void SortModifierKeys_ShouldPreserveOrder_WhenAlreadyInCanonicalOrder()
        {
            // Arrange - already in correct order
            var keys = new List<VirtualKey>
            {
                VirtualKey.LeftWindows,
                VirtualKey.Control,
                VirtualKey.Menu,
                VirtualKey.Shift,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - order should be unchanged
            Assert.AreEqual(VirtualKey.LeftWindows, keys[0]);
            Assert.AreEqual(VirtualKey.Control, keys[1]);
            Assert.AreEqual(VirtualKey.Menu, keys[2]);
            Assert.AreEqual(VirtualKey.Shift, keys[3]);
        }

        // Test that left/right variants of the same modifier maintain their relative
        // position but are grouped correctly relative to other modifier types
        [TestMethod]
        public void SortModifierKeys_ShouldSortCorrectly_WithLeftRightVariants()
        {
            // Arrange - right shift before left control
            var keys = new List<VirtualKey>
            {
                VirtualKey.RightShift,
                VirtualKey.LeftControl,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - Ctrl should come before Shift
            Assert.AreEqual(VirtualKey.LeftControl, keys[0]);
            Assert.AreEqual(VirtualKey.RightShift, keys[1]);
        }

        // Test with a single modifier key (should not throw)
        [TestMethod]
        public void SortModifierKeys_ShouldHandleSingleModifier()
        {
            // Arrange
            var keys = new List<VirtualKey> { VirtualKey.Control };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert
            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(VirtualKey.Control, keys[0]);
        }

        // Test with an empty list (should not throw)
        [TestMethod]
        public void SortModifierKeys_ShouldHandleEmptyList()
        {
            // Arrange
            var keys = new List<VirtualKey>();

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert
            Assert.AreEqual(0, keys.Count);
        }

        // Test GetModifierSortOrder returns correct values
        [TestMethod]
        public void GetModifierSortOrder_ShouldReturnCorrectOrder_ForAllModifierTypes()
        {
            // Win keys should return 0
            Assert.AreEqual(0, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftWindows));
            Assert.AreEqual(0, RemappingHelper.GetModifierSortOrder(VirtualKey.RightWindows));

            // Ctrl keys should return 1
            Assert.AreEqual(1, RemappingHelper.GetModifierSortOrder(VirtualKey.Control));
            Assert.AreEqual(1, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftControl));
            Assert.AreEqual(1, RemappingHelper.GetModifierSortOrder(VirtualKey.RightControl));

            // Alt keys should return 2
            Assert.AreEqual(2, RemappingHelper.GetModifierSortOrder(VirtualKey.Menu));
            Assert.AreEqual(2, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftMenu));
            Assert.AreEqual(2, RemappingHelper.GetModifierSortOrder(VirtualKey.RightMenu));

            // Shift keys should return 3
            Assert.AreEqual(3, RemappingHelper.GetModifierSortOrder(VirtualKey.Shift));
            Assert.AreEqual(3, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftShift));
            Assert.AreEqual(3, RemappingHelper.GetModifierSortOrder(VirtualKey.RightShift));
        }

        // Test that non-modifier keys get the highest sort order (4)
        [TestMethod]
        public void GetModifierSortOrder_ShouldReturnFour_ForNonModifierKeys()
        {
            Assert.AreEqual(4, RemappingHelper.GetModifierSortOrder(VirtualKey.A));
            Assert.AreEqual(4, RemappingHelper.GetModifierSortOrder(VirtualKey.Space));
            Assert.AreEqual(4, RemappingHelper.GetModifierSortOrder(VirtualKey.Enter));
        }

        // Test that two modifiers pressed out of order (Alt then Ctrl) get corrected
        [TestMethod]
        public void SortModifierKeys_ShouldSortCorrectly_WhenAltPressedBeforeCtrl()
        {
            // Arrange
            var keys = new List<VirtualKey>
            {
                VirtualKey.Menu,        // Alt
                VirtualKey.Control,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - Ctrl should come before Alt
            Assert.AreEqual(VirtualKey.Control, keys[0]);
            Assert.AreEqual(VirtualKey.Menu, keys[1]);
        }
    }
}

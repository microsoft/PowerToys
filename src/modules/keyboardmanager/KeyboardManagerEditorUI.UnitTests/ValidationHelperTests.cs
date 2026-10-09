// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class ValidationHelperTests
    {
        private const int VkA = 0x41;
        private const int VkB = 0x42;
        private const int VkC = 0x43;
        private const int VkControl = 0x11;
        private const int VkDisabled = 0x100;

        [TestMethod]
        public void GetOrphanedKeys_ShouldReturnSourceKey_ForSingleKeyRemap()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkA, VkB),
            });

            CollectionAssert.AreEqual(new List<int> { VkA }, new List<int>(orphanedKeys));
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldReturnEmptyList_ForSwappedKeyRemaps()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkA, VkB),
                CreateKeyRemap(VkB, VkA),
            });

            Assert.AreEqual(0, orphanedKeys.Count);
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldReturnSourceKey_ForDisabledKey()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkA, VkDisabled),
            });

            CollectionAssert.AreEqual(new List<int> { VkA }, new List<int>(orphanedKeys));
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldEvaluateReplacementCollection_WhenEditingExistingMapping()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkB, VkA),
            });

            CollectionAssert.AreEqual(new List<int> { VkB }, new List<int>(orphanedKeys));
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldNotTreatKeyInsideShortcutTargetAsAssigned()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkA, VkB),
                CreateShortcutRemap(VkC, VkControl, VkA),
            });

            CollectionAssert.AreEqual(new List<int> { VkA, VkC }, new List<int>(orphanedKeys));
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldNotReturnSourceKey_ForAloneRemap()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkControl, VkA, SingleKeyRemapCondition.Alone),
            });

            Assert.AreEqual(0, orphanedKeys.Count);
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldNotReturnSourceKey_ForAloneDisable()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkControl, VkDisabled, SingleKeyRemapCondition.Alone),
            });

            Assert.AreEqual(0, orphanedKeys.Count);
        }

        [TestMethod]
        public void GetOrphanedKeys_ShouldTreatAloneRemapTargetAsAssigned()
        {
            IReadOnlyList<int> orphanedKeys = ValidationHelper.GetOrphanedKeys(new[]
            {
                CreateKeyRemap(VkA, VkB),
                CreateKeyRemap(VkControl, VkA, SingleKeyRemapCondition.Alone),
            });

            Assert.AreEqual(0, orphanedKeys.Count);
        }

        private static ShortcutKeyMapping CreateKeyRemap(int originalKey, int targetKey, SingleKeyRemapCondition condition = SingleKeyRemapCondition.Always) =>
            new()
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = originalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = targetKey.ToString(CultureInfo.InvariantCulture),
                Condition = condition,
            };

        private static ShortcutKeyMapping CreateShortcutRemap(int originalKey, params int[] targetKeys) =>
            new()
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = originalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = string.Join(";", targetKeys),
            };
    }
}

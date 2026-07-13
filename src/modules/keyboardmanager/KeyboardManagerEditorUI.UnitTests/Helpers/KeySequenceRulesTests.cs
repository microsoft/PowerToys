// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests.Helpers
{
    [TestClass]
    public class KeySequenceRulesTests
    {
        [TestMethod]
        public void CanAppendAllowsEmptyAndModifierPrefixes()
        {
            Assert.IsTrue(KeySequenceRules.CanAppend([], allowChords: false));
            Assert.IsTrue(KeySequenceRules.CanAppend([KeyType.Ctrl], allowChords: false));
            Assert.IsTrue(KeySequenceRules.CanAppend(
                [KeyType.Ctrl, KeyType.Alt, KeyType.Shift, KeyType.Win],
                allowChords: false));
        }

        [TestMethod]
        public void CanAppendStopsAfterStandaloneActionOrCompletedChord()
        {
            Assert.IsFalse(KeySequenceRules.CanAppend([KeyType.Action], allowChords: true));
            Assert.IsFalse(KeySequenceRules.CanAppend([KeyType.Ctrl, KeyType.Action], allowChords: false));
            Assert.IsTrue(KeySequenceRules.CanAppend([KeyType.Ctrl, KeyType.Action], allowChords: true));
            Assert.IsFalse(KeySequenceRules.CanAppend(
                [KeyType.Ctrl, KeyType.Action, KeyType.Action],
                allowChords: true));
        }

        [TestMethod]
        public void EvaluateSelectionRejectsRepeatedModifierType()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl],
                changedIndex: 1,
                KeyType.Ctrl,
                allowChords: false);

            Assert.IsFalse(update.IsValid);
            Assert.AreEqual(KeySequenceError.RepeatedModifier, update.Error);
        }

        [TestMethod]
        public void EvaluateSelectionRejectsModifierAfterAction()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl, KeyType.Action],
                changedIndex: 2,
                KeyType.Shift,
                allowChords: true);

            Assert.IsFalse(update.IsValid);
            Assert.AreEqual(KeySequenceError.ModifierAfterAction, update.Error);
        }

        [TestMethod]
        public void EvaluateSelectionAllowsPrimaryActionAfterFourModifiers()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl, KeyType.Alt, KeyType.Shift, KeyType.Win],
                changedIndex: 4,
                KeyType.Action,
                allowChords: false);

            Assert.IsTrue(update.IsValid);
            Assert.AreEqual(EditorConstants.MaxShortcutSize, update.ResultCount);
        }

        [TestMethod]
        public void EvaluateSelectionAllowsSecondChordAction()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl, KeyType.Action],
                changedIndex: 2,
                KeyType.Action,
                allowChords: true);

            Assert.IsTrue(update.IsValid);
            Assert.AreEqual(3, update.ResultCount);
        }

        [TestMethod]
        public void EvaluateSelectionRequiresChordsForSecondAction()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl, KeyType.Action],
                changedIndex: 2,
                KeyType.Action,
                allowChords: false);

            Assert.IsFalse(update.IsValid);
            Assert.AreEqual(KeySequenceError.ChordsDisabled, update.Error);
        }

        [TestMethod]
        public void EvaluateSelectionRequiresModifierBeforeChord()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Action],
                changedIndex: 1,
                KeyType.Action,
                allowChords: true);

            Assert.IsFalse(update.IsValid);
            Assert.AreEqual(KeySequenceError.ShortcutStartWithModifier, update.Error);
        }

        [TestMethod]
        public void EvaluateSelectionPreservesExistingSecondAction()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl, KeyType.Action, KeyType.Action],
                changedIndex: 1,
                KeyType.Action,
                allowChords: true);

            Assert.IsTrue(update.IsValid);
            Assert.AreEqual(3, update.ResultCount);
        }

        [TestMethod]
        public void EvaluateSelectionTrimsSecondActionWhenChordsAreDisabled()
        {
            KeySequenceUpdate update = KeySequenceRules.EvaluateSelection(
                [KeyType.Ctrl, KeyType.Action, KeyType.Action],
                changedIndex: 1,
                KeyType.Action,
                allowChords: false);

            Assert.IsTrue(update.IsValid);
            Assert.AreEqual(2, update.ResultCount);
        }

        [TestMethod]
        public void GetKeyCountWithoutChordRemovesSecondAction()
        {
            int count = KeySequenceRules.GetKeyCountWithoutChord(
                [KeyType.Ctrl, KeyType.Alt, KeyType.Action, KeyType.Action]);

            Assert.AreEqual(3, count);
        }
    }
}

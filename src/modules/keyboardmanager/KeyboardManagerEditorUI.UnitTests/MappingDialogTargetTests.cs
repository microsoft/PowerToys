// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// S1: the add/edit dialog saves only into the profile it was opened for. A profile switch
    /// while it is open (picker in another editor, cycle hotkey, device auto-switch) must make the
    /// save stop instead of landing in, or briefly rewriting, the newly active profile.
    /// </summary>
    [TestClass]
    public class MappingDialogTargetTests
    {
        [TestMethod]
        public void CanSave_WhenNothingSwitched()
        {
            Assert.IsTrue(MappingDialogTarget.CanSave("Work", "Work", "Work"));
        }

        [TestMethod]
        public void CanSave_IgnoresCase()
        {
            Assert.IsTrue(MappingDialogTarget.CanSave("Work", "work", "WORK"));
        }

        // The settings.json watcher already rebuilt the editor for the new profile.
        [TestMethod]
        public void CannotSave_AfterTheEditorWasRebuiltForAnotherProfile()
        {
            Assert.IsFalse(MappingDialogTarget.CanSave("Work", "Home", "Home"));
        }

        // The engine switched, but the watcher has not rebuilt the editor yet (it is debounced).
        [TestMethod]
        public void CannotSave_WhenTheEngineSwitchedBeforeTheEditorFollowed()
        {
            Assert.IsFalse(MappingDialogTarget.CanSave("Work", "Work", "Home"));
        }

        [TestMethod]
        public void CannotSave_WithoutACapturedTarget()
        {
            Assert.IsFalse(MappingDialogTarget.CanSave(null, "Work", "Work"));
            Assert.IsFalse(MappingDialogTarget.CanSave(string.Empty, "Work", "Work"));
        }

        [TestMethod]
        public void CannotSave_WhenTheCurrentProfileIsUnknown()
        {
            Assert.IsFalse(MappingDialogTarget.CanSave("Work", null, "Work"));
            Assert.IsFalse(MappingDialogTarget.CanSave("Work", "Work", null));
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Whether the editor loads another profile is decided from the profile it has LOADED, never
    /// from what the picker shows.
    /// </summary>
    [TestClass]
    public class ProfileReloadDecisionTests
    {
        // There is no picker argument at all: the picker cannot take part in this decision.
        [TestMethod]
        public void OnDiskChange_LoadedDiffersFromActive_ShowsActive()
        {
            Assert.AreEqual(ProfileReloadAction.ShowActive, ProfileReloadDecision.OnDiskChange("default", "Work"));
        }

        [TestMethod]
        public void OnDiskChange_LoadedIsActive_DoesNothing()
        {
            Assert.AreEqual(ProfileReloadAction.None, ProfileReloadDecision.OnDiskChange("Work", "Work"));
            Assert.AreEqual(ProfileReloadAction.None, ProfileReloadDecision.OnDiskChange("Work", "work"));
        }

        // settings.json could not be read: do not act on a guess; the caller tries again.
        [TestMethod]
        public void OnDiskChange_ActiveUnknown_DoesNothing()
        {
            Assert.AreEqual(ProfileReloadAction.None, ProfileReloadDecision.OnDiskChange("default", null));
        }

        // The engine switched to Work a moment ago and the user then picked Work, before the editor
        // followed. Comparing the selection with the active profile used to end here with the picker
        // on Work and default's rows still loaded.
        [TestMethod]
        public void OnPickerSelection_SelectedIsActiveButNotLoaded_ShowsActive()
        {
            Assert.AreEqual(ProfileReloadAction.ShowActive, ProfileReloadDecision.OnPickerSelection("default", "Work", "Work"));
            Assert.AreEqual(ProfileReloadAction.ShowActive, ProfileReloadDecision.OnPickerSelection("default", "work", "WORK"));
        }

        [TestMethod]
        public void OnPickerSelection_SelectedIsLoaded_DoesNothing()
        {
            Assert.AreEqual(ProfileReloadAction.None, ProfileReloadDecision.OnPickerSelection("Work", "Work", "Work"));
            Assert.AreEqual(ProfileReloadAction.None, ProfileReloadDecision.OnPickerSelection("Work", "work", "default"));
        }

        [TestMethod]
        public void OnPickerSelection_AnotherProfile_Switches()
        {
            Assert.AreEqual(ProfileReloadAction.SwitchTo, ProfileReloadDecision.OnPickerSelection("default", "Home", "default"));
            Assert.AreEqual(ProfileReloadAction.SwitchTo, ProfileReloadDecision.OnPickerSelection("default", "Home", "Work"));
        }

        // With the active profile unknown, a selection other than the loaded profile is a switch.
        [TestMethod]
        public void OnPickerSelection_ActiveUnknown_Switches()
        {
            Assert.AreEqual(ProfileReloadAction.SwitchTo, ProfileReloadDecision.OnPickerSelection("default", "Home", null));
        }
    }
}

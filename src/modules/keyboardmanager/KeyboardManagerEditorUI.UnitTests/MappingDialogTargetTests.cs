// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// The add/edit dialog saves only into the profile it was opened for. A profile switch
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

        // The save path decides the target before anything is written. Opened for Work, the
        // editor and both fresh services now show Home: nothing may be written at all.
        [TestMethod]
        public void Save_WritesNothing_AfterASwitchToAnotherProfile()
        {
            var target = MappingDialogTarget.Capture("Work");
            var writer = new RecordingWriter(MappingWriteResult.Written);

            Assert.AreEqual(MappingSaveResult.ProfileChanged, target.Save("Home", "Home", "Home", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(0, writer.Writes, "The profile check must come before the write.");
        }

        // The engine switched between building the two services: only the candidate sees Home.
        [TestMethod]
        public void Save_WritesNothing_WhenOnlyTheCandidateSeesTheNewProfile()
        {
            var target = MappingDialogTarget.Capture("Work");
            var writer = new RecordingWriter(MappingWriteResult.Written);

            Assert.AreEqual(MappingSaveResult.ProfileChanged, target.Save("Work", "Work", "Home", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(0, writer.Writes);
        }

        // The editor has not followed the switch yet, but the services read the new profile.
        [TestMethod]
        public void Save_WritesNothing_WhenTheServicesSeeTheNewProfileBeforeTheEditor()
        {
            var target = MappingDialogTarget.Capture("Work");
            var writer = new RecordingWriter(MappingWriteResult.Written);

            Assert.AreEqual(MappingSaveResult.ProfileChanged, target.Save("Work", "Home", "Home", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(0, writer.Writes);
        }

        // The target is the one captured when the dialog opened. Switching back to it makes the
        // save go through, into that profile.
        [TestMethod]
        public void Save_Writes_WhenTheCapturedProfileIsActive()
        {
            var target = MappingDialogTarget.Capture("Work");
            var writer = new RecordingWriter(MappingWriteResult.Written);

            Assert.AreEqual("Work", target.Profile);
            Assert.AreEqual(MappingSaveResult.Saved, target.Save("Work", "Work", "Work", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(1, writer.Writes);
            Assert.AreEqual(0, writer.PersistedProfileReads, "A successful write needs no verification lookup.");
        }

        [TestMethod]
        public void Save_WritesNothing_WithoutACapturedTarget()
        {
            var writer = new RecordingWriter(MappingWriteResult.Written);

            Assert.AreEqual(MappingSaveResult.ProfileChanged, MappingDialogTarget.Capture(null).Save("Work", "Work", "Work", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(0, writer.Writes);
        }

        [TestMethod]
        public void Save_ReportsAFailedWrite()
        {
            var writer = new RecordingWriter(MappingWriteResult.Failed);

            Assert.AreEqual(MappingSaveResult.Failed, MappingDialogTarget.Capture("Work").Save("Work", "Work", "Work", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(1, writer.Writes);
        }

        // A switch after the check and before the verification makes the verification read
        // the new profile. That is a profile change, not a generic failure.
        [TestMethod]
        public void Save_ReportsProfileChanged_WhenTheVerificationReadsAnotherProfile()
        {
            var writer = new RecordingWriter(MappingWriteResult.VerifyFailed, persistedProfile: "Home");

            Assert.AreEqual(MappingSaveResult.ProfileChanged, MappingDialogTarget.Capture("Work").Save("Work", "Work", "Work", writer.Write, writer.PersistedProfile));
            Assert.AreEqual(1, writer.PersistedProfileReads);
        }

        [TestMethod]
        public void Save_ReportsFailed_WhenTheVerificationFailsInTheSameProfile()
        {
            var writer = new RecordingWriter(MappingWriteResult.VerifyFailed, persistedProfile: "work");

            Assert.AreEqual(MappingSaveResult.Failed, MappingDialogTarget.Capture("Work").Save("Work", "Work", "Work", writer.Write, writer.PersistedProfile));
        }

        [TestMethod]
        public void Save_ReportsFailed_WhenTheVerifiedProfileIsUnknown()
        {
            var writer = new RecordingWriter(MappingWriteResult.VerifyFailed, persistedProfile: null);

            Assert.AreEqual(MappingSaveResult.Failed, MappingDialogTarget.Capture("Work").Save("Work", "Work", "Work", writer.Write, writer.PersistedProfile));
        }

        private sealed class RecordingWriter
        {
            private readonly MappingWriteResult _result;
            private readonly string _persistedProfile;

            internal RecordingWriter(MappingWriteResult result, string persistedProfile = null)
            {
                _result = result;
                _persistedProfile = persistedProfile;
            }

            internal int Writes { get; private set; }

            internal int PersistedProfileReads { get; private set; }

            internal MappingWriteResult Write()
            {
                Writes++;
                return _result;
            }

            internal string PersistedProfile()
            {
                PersistedProfileReads++;
                return _persistedProfile;
            }
        }
    }
}

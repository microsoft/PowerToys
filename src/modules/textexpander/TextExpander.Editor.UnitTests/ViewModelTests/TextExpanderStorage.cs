// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class TextExpanderStorage
    {
        private const string Current = TextExpanderSnippetFileNames.Current;
        private string _root;
        private string _local;
        private TestStorageEnvironment _environment;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-Storage-" + Guid.NewGuid().ToString("N"));
            _local = Path.Combine(_root, "local");
            Directory.CreateDirectory(_local);
            _environment = new TestStorageEnvironment(_root, _local);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_root, recursive: true);
        }

        [TestMethod]
        public void TheDefaultLocationGetsTheStarterInJson()
        {
            Directory.Delete(_local);
            TextExpanderViewModel model = CreateViewModel();

            string file = Path.Combine(_local, Current);
            Assert.AreEqual(file, model.SnippetsFilePath);
            Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(file));
            Assert.AreEqual("!ddate,!ttime,!ddt,!isodate,!mysig,!myemail", string.Join(",", model.Snippets.Select(row => row.Trigger)));
            Assert.AreEqual("Best regards,\nYour name", model.Snippets.Single(row => row.Trigger == "!mysig").Replacement);
            Assert.IsFalse(model.HasSnippetError);
        }

        [TestMethod]
        public void SavesWriteIndentedJsonWithTheExactText()
        {
            string current = Write(_local, Current, SnippetJson.Of("!one=1"));
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.SaveSnippet(null, "!sig", "Best regards,\nYu\t{{date}} \\n"));

            byte[] saved = File.ReadAllBytes(current);
            Assert.AreNotEqual(0xEF, saved[0], "No byte order mark.");
            string text = Encoding.UTF8.GetString(saved);
            StringAssert.StartsWith(text, "{" + Environment.NewLine + "  \"version\": 1,");
            StringAssert.Contains(text, "\"replacement\": \"Best regards,\\nYu\\t{{date}} \\\\n\"");
            Assert.AreEqual("!one=1 | !sig=Best regards,\nYu\t{{date}} \\n", SnippetJson.Listed(current));
        }

        [TestMethod]
        public void SavesKeepWhatThisVersionDoesNotKnow()
        {
            string current = Write(_local, Current, "{ \"version\": 1, \"theme\": \"dark\", \"snippets\": [ { \"trigger\": \"!a\", \"replacement\": \"one\", \"enabled\": false } ] }");
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.SaveSnippet(model.Snippets.Single().Id, "!a", "edited"));
            Assert.IsNull(model.SaveSnippet(null, "!b", "two"));

            using JsonDocument saved = JsonDocument.Parse(File.ReadAllBytes(current));
            Assert.AreEqual("dark", saved.RootElement.GetProperty("theme").GetString());
            JsonElement edited = saved.RootElement.GetProperty("snippets")[0];
            Assert.AreEqual("edited", edited.GetProperty("replacement").GetString());
            Assert.IsFalse(edited.GetProperty("enabled").GetBoolean());
        }

        [TestMethod]
        public void AFileFromANewerVersionIsReadButNotSavedOver()
        {
            string content = "{ \"version\": 2, \"snippets\": [ { \"trigger\": \"!a\", \"replacement\": \"one\" } ] }";
            string current = Write(_local, Current, content);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsFalse(model.HasSnippetError, "Its snippets are listed, and expand.");
            Assert.AreEqual("TextExpander_SnippetFile_NewerVersion", model.SaveSnippet(null, "!b", "two"));
            Assert.AreEqual("TextExpander_SnippetFile_NewerVersion", model.DeleteSnippet(model.Snippets.Single()));

            Assert.AreEqual(content, File.ReadAllText(current));
            Assert.AreEqual("!a", model.Snippets.Single().Trigger, "The list stays as the file is.");
            Assert.AreEqual(1, Directory.GetFiles(_local).Length, "Nothing is written beside it either.");
        }

        [TestMethod]
        public void TextThatCantBeSavedIsReportedAndTheFileIsLeftAsItIs()
        {
            // Half of an emoji in a property Text Expander doesn't use, as a script that cuts text short may leave it.
            string content = "{ \"version\": 1, \"snippets\": [ { \"trigger\": \"!a\", \"replacement\": \"x\", \"note\": \"\\ud83d\" }, { \"trigger\": \"!b\", \"replacement\": \"y\" } ] }";
            string current = Write(_local, Current, content);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsFalse(model.HasSnippetError, "It loads.");
            Assert.AreEqual("TextExpander_SnippetFile_CantSaveText", model.SaveSnippet(null, "!c", "z"));
            Assert.AreEqual("TextExpander_SnippetFile_CantSaveText", model.SaveSnippet(model.Snippets[0].Id, "!a", "edited"));
            Assert.AreEqual("TextExpander_SnippetFile_CantSaveText", model.DeleteSnippet(model.Snippets[1]));

            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(content), File.ReadAllBytes(current));
            Assert.AreEqual(1, Directory.GetFiles(_local).Length, "Nothing is written beside it either.");
            Assert.AreEqual("!a=x | !b=y", string.Join(" | ", model.Snippets.Select(row => row.Trigger + "=" + row.Replacement)), "The list stays as the file is.");

            // Deleting the snippet that holds it leaves nothing that can't be saved, and the file is kept as a backup.
            Assert.IsNull(model.DeleteSnippet(model.Snippets[0]));
            Assert.AreEqual("!b=y", SnippetJson.Listed(current));
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(content), File.ReadAllBytes(Directory.GetFiles(_local, Current + ".backup-*").Single()));
        }

        [TestMethod]
        public void TheFirstSaveOfAHandWrittenFileKeepsItAsABackup()
        {
            string original = "// My snippets\n{ \"snippets\": [ { \"trigger\": \"!a\", \"replacement\": \"one\" } ] }\n";
            string current = Write(_local, Current, original);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.SaveSnippet(null, "!b", "two"));
            string backup = Directory.GetFiles(_local, Current + ".backup-*").Single();
            Assert.AreEqual(original, File.ReadAllText(backup), "Its comment and layout are kept there.");

            // Text Expander wrote what's there now, so the next save has nothing more to keep.
            Assert.IsNull(model.SaveSnippet(null, "!c", "three"));
            Assert.AreEqual(backup, Directory.GetFiles(_local, Current + ".backup-*").Single());
            Assert.AreEqual("!a=one | !b=two | !c=three", SnippetJson.Listed(current));

            // Until it's changed by hand again.
            File.WriteAllText(current, "// Edited\n" + File.ReadAllText(current));
            model.LoadSnippets();
            Assert.IsNull(model.SaveSnippet(null, "!d", "four"));
            Assert.AreEqual(2, Directory.GetFiles(_local, Current + ".backup-*").Length);
        }

        [TestMethod]
        public void ASaveThatFailsLeavesNoTemporaryFile()
        {
            string current = Write(_local, Current, SnippetJson.Of("!a=one"));
            TextExpanderViewModel model = CreateViewModel();

            // Another app has the file open and doesn't let it be replaced.
            using (new FileStream(current, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.IsNotNull(model.SaveSnippet(null, "!b", "two"));
            }

            Assert.AreEqual(0, Directory.GetFiles(_local, "*.tmp-*").Length);
            Assert.AreEqual("!a=one", SnippetJson.Listed(current));
        }

        [TestMethod]
        public void AReplaceThatLeavesNoFileKeepsTheSavedSnippets()
        {
            string current = Write(_local, Current, SnippetJson.Of("!a=one"));
            TextExpanderViewModel model = CreateViewModel();
            Assert.IsNull(model.SaveSnippet(null, "!b", "two"));

            // ERROR_UNABLE_TO_MOVE_REPLACEMENT (1176) with no backup: the file is gone and the saved snippets keep their
            // temporary name, so they're moved into place.
            _environment.FailReplace = target => File.Delete(target);
            Assert.IsNotNull(model.SaveSnippet(null, "!c", "three"));
            Assert.AreEqual("!a=one | !b=two | !c=three", SnippetJson.Listed(current));
            Assert.AreEqual(0, Directory.GetFiles(_local, "*.tmp-*").Length);

            // When that move fails too, they stay where they are.
            model.LoadSnippets();
            _environment.FailReplace = target =>
            {
                File.Delete(target);
                Directory.CreateDirectory(target);
            };
            Assert.IsNotNull(model.SaveSnippet(null, "!d", "four"));
            Assert.AreEqual("!a=one | !b=two | !c=three | !d=four", SnippetJson.Listed(Directory.GetFiles(_local, Current + ".tmp-*").Single()));
        }

        [TestMethod]
        public void ChangesReportedBeforeTheCheckRunsAreCheckedOnce()
        {
            Write(_local, Current, SnippetJson.Of("!a=one"));
            TextExpanderViewModel model = CreateViewModel();
            var queued = new List<Action>();
            model.Dispatch = queued.Add;

            _environment.NotifyFilesChanged();
            _environment.NotifyFilesChanged();
            Assert.AreEqual(1, queued.Count, "The check reads the file when it runs, so one covers both.");

            queued[0]();
            _environment.NotifyFilesChanged();
            Assert.AreEqual(2, queued.Count, "A change after the check starts is checked again.");
        }

        [TestMethod]
        public void AFileAsTextExpanderWritesItIsSavedWithoutABackup()
        {
            Directory.Delete(_local);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.SaveSnippet(null, "!new", "New"));

            CollectionAssert.AreEqual(new[] { Path.Combine(_local, Current) }, Directory.GetFiles(_local), "The starter is as Text Expander writes it.");
        }

        [TestMethod]
        [DataRow("!a=one", "TextExpander_SnippetError_NotJson", DisplayName = "the old text format")]
        [DataRow("{ \"snippets\": [ { \"trigger\": \"!a\" \n", "TextExpander_SnippetError_NotJson", DisplayName = "cut off")]
        [DataRow("{ \"version\": 1 }", "TextExpander_SnippetError_NoList", DisplayName = "no list")]
        public void AFileThatIsNotASnippetsFileIsReportedAndNeverSavedOver(string content, string error)
        {
            string current = Write(_local, Current, content);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsTrue(model.HasSnippetError);
            Assert.AreEqual("TextExpander_SnippetError_CantOpen", model.SnippetErrorTitle);
            Assert.AreEqual(error, model.SnippetError);
            Assert.IsFalse(model.IsSnippetsFileUnavailable, "It's there, so Use a snippets file… isn't offered as the way out.");
            Assert.IsFalse(model.CanAddSnippets);
            Assert.AreEqual(0, model.Snippets.Count);

            Assert.IsNotNull(model.SaveSnippet(null, "!new", "New"));
            Assert.AreEqual(content, File.ReadAllText(current), "A file that isn't read is never written over.");
        }

        [TestMethod]
        public void TheLineWithTheMistakeIsNamed()
        {
            Write(_local, Current, "{\n  \"snippets\": [\n    { \"trigger\": \"!a\" \"replacement\": \"one\" }\n  ]\n}");
            TextExpanderViewModel model = CreateViewModel(getResourceString: key => key == "TextExpander_SnippetError_NotJson" ? "Line {0}" : key);

            Assert.AreEqual("Line 3", model.SnippetError);
        }

        [TestMethod]
        public void OpenSnippetsFileOpensTheFileInUse()
        {
            string file = Write(_local, Current, SnippetJson.Of("!local=Local"));
            string opened = null;
            TextExpanderViewModel model = CreateViewModel(openFile: path => opened = path);

            Assert.IsNull(model.OpenSnippetsFile());
            Assert.AreEqual(file, opened);
        }

        [TestMethod]
        public void OpenSnippetsFileReportsLaunchAndMissingFileFailures()
        {
            string current = Write(_local, Current, SnippetJson.Of("!local=Local"));
            TextExpanderViewModel model = CreateViewModel(openFile: _ => throw new InvalidOperationException("Injected launch failure."));

            Assert.AreEqual("TextExpander_SnippetFile_OpenFailed", model.OpenSnippetsFile());

            File.Delete(current);
            Assert.AreEqual("TextExpander_SnippetFile_OpenMissing", model.OpenSnippetsFile());
        }

        [TestMethod]
        public void AJsonFileWithNoAppSetForItOpensInNotepadWithoutAsking()
        {
            // Opened through the shell, it brought up Windows' "How do you want to open this file?" instead.
            string file = @"C:\Users\alex\OneDrive\My snippets\PowerToysTextExpanderSnippets.json";
            var started = new List<ProcessStartInfo>();
            var lookedUp = new List<string>();

            TextExpanderViewModel.OpenFile(file, started.Add, extension =>
            {
                lookedUp.Add(extension);
                return false;
            });

            Assert.AreEqual(".json", lookedUp.Single());
            ProcessStartInfo info = started.Single();
            Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "notepad.exe"), info.FileName);
            CollectionAssert.AreEqual(new[] { file }, info.ArgumentList.ToArray());
            Assert.IsFalse(info.UseShellExecute);
        }

        [TestMethod]
        public void AnAppSetForJsonThatGoesAwayBeforeTheFileOpensLeavesItToNotepad()
        {
            string file = @"C:\Users\alex\OneDrive\My snippets\PowerToysTextExpanderSnippets.json";
            var started = new List<ProcessStartInfo>();

            TextExpanderViewModel.OpenFile(
                file,
                info =>
                {
                    started.Add(info);
                    if (info.UseShellExecute)
                    {
                        throw new Win32Exception(1155, "No application is associated with the specified file for this operation.");
                    }
                },
                _ => true);

            Assert.AreEqual(2, started.Count);
            Assert.AreEqual(file, started[0].FileName, "The app Windows uses for the file type comes first.");
            Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "notepad.exe"), started[1].FileName);
            CollectionAssert.AreEqual(new[] { file }, started[1].ArgumentList.ToArray());
            Assert.IsFalse(started[1].UseShellExecute);
        }

        [TestMethod]
        public void TheAppForAFileTypeIsLookedUpWithoutOpenWithStandingInForNone()
        {
            Assert.IsTrue(TextExpanderFileAssociation.HasDefaultApp(".exe"), "Windows runs .exe files itself.");
            Assert.IsFalse(TextExpanderFileAssociation.HasDefaultApp(".te-no-app-" + Guid.NewGuid().ToString("N")), "No app is set for a made-up type.");
        }

        [TestMethod]
        [DataRow(@"C:\Users\alex\Snippets.JSON", true)]
        [DataRow(@"C:\Users\alex\snippets.cmd", false)]
        [DataRow(@"C:\Users\alex\snippets.js", false)]
        [DataRow(@"C:\Users\alex\snippets.json.lnk", false)]
        [DataRow(@"C:\Users\alex\snippets", false)]
        public void OnlyAJsonFileOpensInTheAppForItsType(string file, bool shell)
        {
            var started = new List<ProcessStartInfo>();

            TextExpanderViewModel.OpenFile(file, started.Add, _ => true);

            ProcessStartInfo info = started.Single();
            Assert.AreEqual(shell, info.UseShellExecute, "Opening a script or a shortcut in the app for its type would run it.");
            Assert.AreEqual(shell ? file : Path.Combine(Environment.SystemDirectory, "notepad.exe"), info.FileName);
            CollectionAssert.AreEqual(shell ? Array.Empty<string>() : new[] { file }, info.ArgumentList.ToArray());
        }

        [TestMethod]
        public void OtherLaunchFailuresAreNotRetriedInNotepad()
        {
            int starts = 0;

            Assert.ThrowsException<Win32Exception>(() => TextExpanderViewModel.OpenFile(
                @"C:\snippets.json",
                _ =>
                {
                    starts++;
                    throw new Win32Exception(5);
                },
                _ => true));

            Assert.AreEqual(1, starts);
        }

        [TestMethod]
        public void ChangesToTheFileReloadAndRaiseRevisionForOpenEditors()
        {
            string current = Write(_local, Current, SnippetJson.Of("!first=First"));
            TextExpanderViewModel model = CreateViewModel();
            int revisionNotifications = 0;
            model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision))
                {
                    revisionNotifications++;
                }
            };
            model.Dispatch = action => action();

            int firstRevision = model.SnippetRevision;
            File.WriteAllText(current, SnippetJson.Of("!changed=Changed"));
            Assert.AreEqual("!first", model.Snippets.Single().Trigger, "The list shows what was read until the watcher reloads.");
            _environment.NotifyFilesChanged();

            Assert.AreEqual("!changed", model.Snippets.Single().Trigger);
            Assert.IsTrue(model.SnippetRevision > firstRevision);
            Assert.AreEqual(1, revisionNotifications, "The page uses this notification to invalidate its editor and close its Prompt window.");
        }

        [TestMethod]
        public void ASaveThatDeletesThePCsFileAndRenamesACopyIntoPlaceIsNotLostToTheStarter()
        {
            string current = Write(_local, Current, SnippetJson.Of("!mine=Mine"));
            TextExpanderViewModel model = CreateViewModel();
            int revisionNotifications = 0;
            model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision))
                {
                    revisionNotifications++;
                }
            };
            model.Dispatch = action => action();
            int revision = model.SnippetRevision;

            // An editor that saves by writing a copy, deleting the file and renaming the copy into place. The watcher
            // and Refresh both read while the file is away; neither writes the starter, which would make the rename fail.
            string copy = current + ".saving";
            File.WriteAllText(copy, SnippetJson.Of("!saved=Saved"));
            File.Delete(current);
            _environment.NotifyFilesChanged();
            model.LoadSnippets();

            Assert.IsFalse(File.Exists(current), "No starter is written while the file is away.");
            Assert.IsTrue(model.IsWaitingForSnippetsFile);
            Assert.IsFalse(model.HasSnippetError);
            Assert.AreEqual("!mine", model.Snippets.Single().Trigger, "The list stays as it was.");
            Assert.AreEqual(revision, model.SnippetRevision, "An open snippet dialog isn't disturbed.");

            File.Move(copy, current, overwrite: false);
            _environment.NotifyFilesChanged();

            Assert.AreEqual("!saved", model.Snippets.Single().Trigger);
            Assert.IsFalse(model.IsWaitingForSnippetsFile);
            Assert.AreEqual(1, revisionNotifications);
        }

        [TestMethod]
        public void ThePCsFileDeletedForGoodComesBackWithTheListWhenASnippetIsSaved()
        {
            string current = Write(_local, Current, SnippetJson.Of("!mine=Mine"));
            TextExpanderViewModel model = CreateViewModel();
            model.Dispatch = action => action();

            File.Delete(current);
            _environment.NotifyFilesChanged();
            Assert.IsFalse(File.Exists(current));
            Assert.IsTrue(model.IsWaitingForSnippetsFile);

            Assert.IsNull(model.SaveSnippet(null, "!added", "Added"));
            Assert.AreEqual("!mine=Mine | !added=Added", SnippetJson.Listed(current));
            Assert.IsFalse(model.IsWaitingForSnippetsFile);

            // The editor starting again with no file writes the starter.
            File.Delete(current);
            TextExpanderViewModel restarted = CreateViewModel();
            Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(current));
            Assert.AreEqual(6, restarted.Snippets.Count);
            Assert.IsFalse(restarted.IsWaitingForSnippetsFile);
        }

        [TestMethod]
        public void OwnAtomicSavesDoNotReloadOrCreateFalseConflicts()
        {
            string current = Write(_local, Current, SnippetJson.Of("!one=1"));
            TextExpanderViewModel model = CreateViewModel();
            model.Dispatch = action => action();

            Assert.IsNull(model.SaveSnippet(null, "!two", "2"));
            int savedRevision = model.SnippetRevision;
            byte[] savedBytes = File.ReadAllBytes(current);

            _environment.NotifyFilesChanged();
            _environment.NotifyFilesChanged();

            Assert.AreEqual(savedRevision, model.SnippetRevision, "Watcher events for the editor's own bytes must be ignored.");
            CollectionAssert.AreEqual(savedBytes, File.ReadAllBytes(current));
            Assert.IsNull(model.SaveSnippet(null, "!three", "3"), "Ignoring an own-save event must not manufacture an external-change conflict.");
            Assert.AreEqual(3, model.Snippets.Count);

            int ownSaveRevision = model.SnippetRevision;
            File.WriteAllText(current, SnippetJson.Of("!external=Elsewhere"));
            _environment.NotifyFilesChanged();
            Assert.IsTrue(model.SnippetRevision > ownSaveRevision);
            Assert.AreEqual("!external", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void DeleteConfirmedAcrossAWatcherReloadIsRefusedInsteadOfDeletingTheRenumberedRow()
        {
            string current = Write(_local, Current, SnippetJson.Of("!a=A", "!b=B"));
            TextExpanderViewModel model = CreateViewModel();
            model.Dispatch = action => action();

            // What the page holds while it asks to confirm deleting !b.
            TextExpanderSnippetViewModel confirming = model.Snippets.Single(row => row.Trigger == "!b");

            // Meanwhile a sync puts !c first and the watcher reloads, which numbers the rows again.
            string synced = SnippetJson.Of("!c=C", "!a=A", "!b=B");
            File.WriteAllText(current, synced);
            _environment.NotifyFilesChanged();
            Assert.AreEqual("!a", model.Snippets.Single(row => row.Id == confirming.Id).Trigger);

            Assert.AreEqual("TextExpander_FileChangedBeforeSave", model.DeleteSnippet(confirming));
            Assert.AreEqual(synced, File.ReadAllText(current));
            Assert.AreEqual("!c,!a,!b", string.Join(',', model.Snippets.Select(row => row.Trigger)));

            // Deleting from the reloaded list removes exactly !b.
            Assert.IsNull(model.DeleteSnippet(model.Snippets.Single(row => row.Trigger == "!b")));
            Assert.AreEqual("!c=C | !a=A", SnippetJson.Listed(current));
        }

        [TestMethod]
        public void SettingsProcessChangesRefreshTheFileAndMatchingRules()
        {
            string local = Write(_local, Current, SnippetJson.Of("!old=Old"));
            string next = Write(Path.Combine(_root, "next"), "work.json", SnippetJson.Of("!next=Next"));
            TextExpanderSettings latest = EditorSettings.For(next);
            latest.Properties.CaseSensitive = false;
            latest.Properties.RequireWordBoundary = false;
            using TextExpanderViewModel model = CreateViewModel(latest: () => latest);

            model.RefreshSettings();

            Assert.AreEqual(next, model.SnippetsPath);
            Assert.AreEqual(next, model.SnippetsFilePath);
            Assert.AreEqual("!next", model.Snippets.Single().Trigger);
            Assert.IsFalse(model.CaseSensitive);
            Assert.IsFalse(model.RequireWordBoundary);
            Assert.IsTrue(File.Exists(local));
        }

        [TestMethod]
        public void SettingsChangedBeforeWatcherRegistrationAreAppliedWithoutAnotherEvent()
        {
            Write(_local, Current, SnippetJson.Of("!Hello=Original"));
            TextExpanderSettings latest = EditorSettings.For(string.Empty);
            Mock<TextExpanderSettingsUtils> store = EditorSettings.Store(EditorSettings.For(string.Empty), () => latest);
            _environment.SettingsWatchFolder = Path.GetDirectoryName(store.Object.GetSettingsFilePath(TextExpanderSettings.ModuleName));
            using var model = new TextExpanderViewModel(store.Object, false, key => key, _environment);
            model.Dispatch = action => action();
            Assert.IsTrue(model.CaseSensitive);
            Assert.IsNull(model.ValidateSnippet(null, "!HELLO"));

            // Settings publishes new rules while the page is being constructed, before its Loaded handler watches them.
            latest = EditorSettings.For(string.Empty);
            latest.Properties.CaseSensitive = false;
            latest.Properties.RequireWordBoundary = false;
            model.WatchSettings();

            Assert.IsFalse(model.CaseSensitive);
            Assert.IsFalse(model.RequireWordBoundary);
            Assert.IsNotNull(model.ValidateSnippet(null, "!HELLO"), "Validation must use the rules the engine now uses.");
        }

        [TestMethod]
        public void UnrelatedEngineOptionsDoNotInvalidateAnOpenEditor()
        {
            Write(_local, Current, SnippetJson.Of("!old=Old"));
            TextExpanderSettings latest = EditorSettings.For(string.Empty);
            latest.Properties.InjectionBackend.Value = "type";
            using TextExpanderViewModel model = CreateViewModel(latest: () => latest);
            int revision = model.SnippetRevision;
            TextExpanderSnippetViewModel row = model.Snippets.Single();

            model.RefreshSettings();

            Assert.AreEqual(revision, model.SnippetRevision);
            Assert.AreSame(row, model.Snippets.Single());
        }

        [TestMethod]
        public void MatchingRuleChangesRefreshRowsWithoutInvalidatingAnOpenDraft()
        {
            Write(_local, Current, SnippetJson.Of("!a=First", "!A=Last", "!abc=Longer"));
            TextExpanderSettings latest = EditorSettings.For(string.Empty);
            latest.Properties.CaseSensitive = false;
            latest.Properties.RequireWordBoundary = false;
            using TextExpanderViewModel model = CreateViewModel(latest: () => latest);
            int revision = model.SnippetRevision;
            TextExpanderSnippetViewModel row = model.Snippets.First();
            Assert.IsFalse(row.HasWarning);

            model.RefreshSettings();

            Assert.AreEqual(revision, model.SnippetRevision);
            Assert.AreSame(row, model.Snippets.First());
            Assert.IsTrue(row.HasWarning);
            Assert.IsTrue(model.Snippets.Last().HasWarning);
            Assert.IsFalse(model.CaseSensitive);
            Assert.IsFalse(model.RequireWordBoundary);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AQueuedReadFailureIsRetriedWithoutAnotherFileChange(bool invalidJson)
        {
            // Start the real watcher while this custom folder is absent, so its polling path is the only notification
            // source. UI work is queued and executed separately, as DispatcherQueue does in the actual page.
            string folder = Path.Combine(_root, "arrives-later");
            string path = Path.Combine(folder, Current);
            _environment.PollFiles = true;
            var settings = EditorSettings.Store(EditorSettings.For(path));
            using var model = new TextExpanderViewModel(settings.Object, false, key => key, _environment);
            var queued = new ConcurrentQueue<Action>();
            model.Dispatch = queued.Enqueue;
            Write(folder, Current, SnippetJson.Of("!old=Old"));
            Assert.IsTrue(SpinWait.SpinUntil(() => !queued.IsEmpty, TimeSpan.FromSeconds(5)));
            RunQueuedReads(queued);
            Assert.AreEqual("!old", model.Snippets.Single().Trigger);
            Assert.IsFalse(model.HasSnippetError);

            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            RunQueuedReads(queued);
            int revision = model.SnippetRevision;
            string updated = invalidJson ? "{" : SnippetJson.Of("!new=New");
            using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(updated);
                writer.Write(bytes);
                writer.Flush(flushToDisk: true);
                Assert.IsTrue(SpinWait.SpinUntil(() => !queued.IsEmpty, TimeSpan.FromSeconds(5)));

                // Queueing did not read the file. Let the poller observe the final stamp before the UI attempts the read.
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                Assert.IsTrue(queued.TryDequeue(out Action read));
                read();
                Assert.AreEqual(revision, model.SnippetRevision);
                Assert.AreEqual("!old", model.Snippets.Single().Trigger);
                Assert.IsFalse(model.HasSnippetError, "A temporary read conflict keeps the readable list.");
            }

            Assert.IsTrue(SpinWait.SpinUntil(() => !queued.IsEmpty, TimeSpan.FromSeconds(5)), "The failed byte read must be retried even though the stamp is unchanged.");
            RunQueuedReads(queued);
            Assert.AreEqual(invalidJson, model.HasSnippetError);
            if (!invalidJson)
            {
                Assert.AreEqual("!new", model.Snippets.Single().Trigger);
                Assert.IsTrue(model.CanAddSnippets);
            }

            // Bytes that were read successfully consume the pending state, even if they aren't valid JSON. The error
            // stays visible without repeatedly reparsing it, and an unchanged healthy library stays idle too.
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            RunQueuedReads(queued);
            int loadedRevision = model.SnippetRevision;
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            Assert.IsTrue(queued.IsEmpty, "Neither healthy content nor a parsed file error needs another byte-read retry.");
            Assert.AreEqual(loadedRevision, model.SnippetRevision);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RecoveredSettingsClearOnlyTheirOwnErrorWithoutReloadingTheLibrary(bool invalidLibrary)
        {
            Write(_local, Current, invalidLibrary ? "{" : SnippetJson.Of("!old=Old"));
            bool failRead = false;
            using TextExpanderViewModel model = CreateViewModel(latest: () => failRead
                ? throw new InvalidOperationException("Transient settings read failure.")
                : EditorSettings.For(string.Empty));
            int revision = model.SnippetRevision;
            TextExpanderSnippetViewModel[] rows = model.Snippets.ToArray();
            string libraryError = model.SnippetError;
            var notifications = new List<string>();
            model.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

            model.RefreshSettings();
            Assert.AreEqual(0, notifications.Count, "A healthy unchanged settings read doesn't rebuild the list.");
            failRead = true;
            model.RefreshSettings();
            Assert.IsTrue(model.HasSnippetError);
            Assert.IsFalse(model.CanAddSnippets);
            if (invalidLibrary)
            {
                Assert.AreEqual(libraryError, model.SnippetError, "Settings errors must not overwrite a real library error.");
            }

            failRead = false;
            notifications.Clear();
            model.RefreshSettings();
            Assert.AreEqual(invalidLibrary, model.HasSnippetError);
            Assert.AreEqual(!invalidLibrary, model.CanAddSnippets);
            Assert.AreEqual(libraryError, model.SnippetError);
            Assert.AreEqual(revision, model.SnippetRevision, "Recovering settings doesn't invalidate an open snippet draft.");
            CollectionAssert.AreEqual(rows, model.Snippets.ToArray());
            CollectionAssert.Contains(notifications, nameof(model.CanAddSnippets));
        }

        private static void RunQueuedReads(ConcurrentQueue<Action> queued)
        {
            while (queued.TryDequeue(out Action read))
            {
                read();
            }
        }

        [TestMethod]
        public void FailedSettingsRefreshIsVisibleAndPreservesTheLoadedLibrary()
        {
            Write(_local, Current, SnippetJson.Of("!old=Old"));
            using TextExpanderViewModel model = CreateViewModel(latest: () => throw new InvalidOperationException("The settings file could not be read consistently."));
            int revision = model.SnippetRevision;

            model.RefreshSettings();

            Assert.IsTrue(model.HasSnippetError);
            Assert.AreEqual("!old", model.Snippets.Single().Trigger);
            Assert.AreEqual(revision, model.SnippetRevision);
        }

        private TextExpanderViewModel CreateViewModel(
            Action<string> openFile = null,
            Func<TextExpanderSettings> latest = null,
            Func<string, string> getResourceString = null)
        {
            Mock<TextExpanderSettingsUtils> store = EditorSettings.Store(EditorSettings.For(string.Empty), latest);
            return new(store.Object, isElevated: false, getResourceString ?? (key => key), _environment, openFile);
        }

        private static string Write(string folder, string name, string content)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
            return path;
        }

        private sealed class TestStorageEnvironment : ITextExpanderStorageEnvironment
        {
            private readonly string _root;
            private Action _filesChanged;

            public TestStorageEnvironment(string root, string defaultSnippetsFolder)
            {
                _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                DefaultSnippetsFolder = defaultSnippetsFolder;
            }

            public string DefaultSnippetsFolder { get; }

            public bool PollFiles { get; set; }

            public string SettingsWatchFolder { get; set; }

            public string ExpandEnvironmentVariables(string value) => value;

            public bool FileExists(string path) => File.Exists(Guard(path));

            public void CreateDirectory(string path) => Directory.CreateDirectory(Guard(path));

            public void WriteNewFile(string path, byte[] bytes)
            {
                using var stream = new FileStream(Guard(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes, 0, bytes.Length);
            }

            public void MoveFile(string source, string destination) => File.Move(Guard(source), Guard(destination), overwrite: false);

            /// <summary>Gets or sets what a replace does instead, given its target, before it fails.</summary>
            public Action<string> FailReplace { get; set; }

            public void ReplaceFile(string source, string destination, string backup)
            {
                if (FailReplace != null)
                {
                    FailReplace(Guard(destination));
                    throw new IOException("Unable to move the replacement file to the file to be replaced.", unchecked((int)0x80070498));
                }

                File.Replace(Guard(source), Guard(destination), backup == null ? null : Guard(backup), ignoreMetadataErrors: true);
            }

            public HandleBoundFileDeletionResult DeleteOwnedFile(string path, byte[] expectedBytes)
                => HandleBoundFileDeletion.TryDelete(Guard(path), expectedBytes);

            public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed, Func<bool> needsRetry = null)
            {
                if (PollFiles)
                {
                    return new TextExpanderStorageEnvironment(TimeSpan.FromMilliseconds(20)).WatchForFiles(Guard(folder), fileNames, changed, needsRetry);
                }

                // This watch only keeps a callback; a settings watch must not access the user's actual folder.
                if (!string.Equals(folder, SettingsWatchFolder, StringComparison.OrdinalIgnoreCase))
                {
                    Guard(folder);
                }

                _filesChanged = changed;
                return new CallbackDisposable(() =>
                {
                    if (ReferenceEquals(_filesChanged, changed))
                    {
                        _filesChanged = null;
                    }
                });
            }

            public void NotifyFilesChanged() => _filesChanged?.Invoke();

            private string Guard(string path)
            {
                string full = Path.GetFullPath(path);
                if (!full.Equals(_root, StringComparison.OrdinalIgnoreCase)
                    && !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail("The test touched a path outside its temporary folder: " + full);
                }

                return full;
            }
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private readonly Action _dispose;

            public CallbackDisposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose() => _dispose();
        }
    }
}

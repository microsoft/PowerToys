// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    /// <summary>
    /// The "Sync snippets across PCs" setup. Every test runs against folders under a temporary
    /// directory through <see cref="TextExpanderSyncTestEnvironment"/>, never a real OneDrive.
    /// </summary>
    [TestClass]
    public class TextExpanderSync
    {
        private const string LocalSnippets = "!mysig=Best regards\n!myemail=you@example.com\n!isodate={{isodate}}\n";
        private const string WorkAccount = "OneDrive \u2013 Contoso (work or school)";
        private const string StandardSetting = @"%OneDriveCommercial%\PowerToys\TextExpander";

        private static readonly string[] TwoAccountNames = { "OneDrive \u2013 Personal", WorkAccount };
        private static readonly string[] TwoAccountVariables = { "%OneDriveConsumer%", "%OneDriveCommercial%" };
        private static readonly string[] BothFileNames = { "PowerToysTextExpanderSnippets.txt", "snippets.txt" };

        private string _root;
        private string _local;
        private string _personal;
        private string _work;

        // "snippets.txt" is the name earlier versions used; most tests keep it, since existing
        // libraries have it. A file Text Expander creates gets the current name.
        private string LocalFile => Path.Combine(_local, "snippets.txt");

        private string LocalNewFile => Path.Combine(_local, "PowerToysTextExpanderSnippets.txt");

        private string WorkFolder => Path.Combine(_work, "PowerToys", "TextExpander");

        private string WorkFile => Path.Combine(WorkFolder, "snippets.txt");

        private string WorkNewFile => Path.Combine(WorkFolder, "PowerToysTextExpanderSnippets.txt");

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpanderSync-" + Guid.NewGuid().ToString("N"));
            _local = Path.Combine(_root, "Local", "TextExpander");
            _personal = Path.Combine(_root, "Profile", "OneDrive");
            _work = Path.Combine(_root, "Profile", "OneDrive - Contoso");
            Directory.CreateDirectory(_local);
            Directory.CreateDirectory(_personal);
            Directory.CreateDirectory(_work);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_root, recursive: true);
        }

        [TestMethod]
        public void FirstPcCopiesThisPcsSnippetsBeforePointingTextExpanderAtOneDrive()
        {
            byte[] local = WriteFile(LocalFile, LocalSnippets, bom: true);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;
            bool fileExistedWhenFolderChanged = false;
            model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.SnippetsPath))
                {
                    fileExistedWhenFolderChanged = File.Exists(WorkNewFile);
                }
            };

            StartSetup(model);

            // QA-8: one account, so setup doesn't ask which.
            Assert.IsFalse(model.Sync.HasMultipleAccounts);
            Assert.AreEqual(WorkAccount, model.Sync.Accounts[model.Sync.SelectedAccountIndex].DisplayName);
            Assert.IsTrue(model.Sync.IsFirstPcSelected);
            Assert.AreEqual("Copy my 3 snippets to OneDrive.", model.Sync.FirstPcDescription);
            Assert.IsFalse(model.Sync.HasConflict);
            Assert.IsTrue(model.Sync.CanCopy);

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(local, File.ReadAllBytes(WorkNewFile));
            Assert.IsFalse(File.Exists(WorkFile), "A first copy in OneDrive gets the current name.");
            Assert.IsTrue(fileExistedWhenFolderChanged, "The folder must change only after the snippet file exists there.");
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
            Assert.AreEqual(WorkNewFile, model.SnippetsFilePath);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.AreEqual("Syncing with " + WorkAccount + " \u00B7 3 snippets", model.Sync.SyncedStatusText);
            Assert.AreEqual("Your snippets are now syncing with " + WorkAccount + ".", announced);
            Assert.IsFalse(model.Sync.ShowDetails, "Announced only: nothing stays under the toggle.");
            CollectionAssert.AreEqual(local, File.ReadAllBytes(LocalFile), "This PC's file stays as it was.");
            Assert.AreEqual(1, Directory.GetFiles(WorkFolder).Length, "No temporary files may be left behind.");

            // The toggle settles On, and showing that state starts nothing.
            Assert.IsTrue(model.Sync.IsSyncOn);
            Assert.IsFalse(model.Sync.IsSetupVisible);
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.AreEqual(1, Directory.GetFiles(WorkFolder).Length);
        }

        [TestMethod]
        public void ConflictDefaultsToTheOneDriveSnippetsAndChangesNoFile()
        {
            WriteFile(LocalFile, LocalSnippets);
            byte[] oneDrive = WriteFile(WorkFile, "!a=1\n!b=2\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));

            StartSetup(model);
            Assert.IsTrue(model.Sync.HasConflict);
            Assert.AreEqual("OneDrive already has 2 snippets.", model.Sync.ConflictTitle);
            Assert.AreEqual("Use the 2 snippets in OneDrive", model.Sync.UseOneDriveChoiceText);
            Assert.AreEqual("Replace with this PC's 3 snippets", model.Sync.ReplaceChoiceText);
            Assert.AreEqual(TextExpanderSyncViewModel.UseOneDriveChoice, model.Sync.ConflictChoiceIndex);
            Assert.AreEqual("TextExpander_Sync_ContinueButton", model.Sync.FirstPcActionText);

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(WorkFile));
            Assert.AreEqual(1, Directory.GetFiles(WorkFolder).Length);
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
        }

        [TestMethod]
        public void ReplaceKeepsTheOneDriveFileAsAUniqueBackup()
        {
            byte[] local = WriteFile(LocalFile, LocalSnippets);
            byte[] oneDrive = WriteFile(WorkFile, "!a=1\n");
            string existingBackup = Path.Combine(WorkFolder, "snippets.backup.txt");
            File.WriteAllText(existingBackup, "an older backup");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));

            StartSetup(model);
            Assert.AreEqual("OneDrive already has 1 snippet.", model.Sync.ConflictTitle);
            model.Sync.ConflictChoiceIndex = TextExpanderSyncViewModel.ReplaceOneDriveChoice;

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(local, File.ReadAllBytes(WorkFile));
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(Path.Combine(WorkFolder, "snippets.backup (2).txt")));
            Assert.AreEqual("an older backup", File.ReadAllText(existingBackup));
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
        }

        [TestMethod]
        public void KeepBothUsesOneDrivesFileAndSavesThisPcsBesideIt()
        {
            byte[] local = WriteFile(LocalFile, LocalSnippets);
            byte[] oneDrive = WriteFile(WorkFile, "!a=1\n!b=2\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));

            StartSetup(model);
            Assert.AreEqual("Use OneDrive's file, and save this PC's as snippets (TESTPC).txt.", model.Sync.KeepBothDescription);
            model.Sync.ConflictChoiceIndex = TextExpanderSyncViewModel.KeepBothChoice;

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(WorkFile));
            CollectionAssert.AreEqual(local, File.ReadAllBytes(Path.Combine(WorkFolder, "snippets (TESTPC).txt")));
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
        }

        [TestMethod]
        public void AFileThatAppearsAfterTheCheckIsNeverOverwritten()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));
            StartSetup(model);
            Assert.IsFalse(model.Sync.HasConflict);

            byte[] oneDrive = WriteFile(WorkFile, "!late=arrival\n");

            Assert.AreEqual("TextExpander_Sync_ChooseWhichToKeep", model.Sync.CopyToOneDrive());
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(WorkFile));
            Assert.IsTrue(model.Sync.HasConflict);
            Assert.AreEqual(string.Empty, model.SnippetsPath);
        }

        [TestMethod]
        public void AnotherPcLooksFirstAndThenUsesTheSnippetsItFound()
        {
            byte[] local = WriteFile(LocalFile, LocalSnippets);
            WriteFile(WorkFile, "!a=1\n!b=2\n");
            var changed = new DateTime(2026, 10, 16, 16, 12, 0, DateTimeKind.Local);
            File.SetLastWriteTime(WorkFile, changed);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;
            StartSetup(model);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsTrue(model.Sync.IsFound);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.AreEqual(0, environment.Listed.Count, "Found in the standard folder, so OneDrive isn't searched.");
            Assert.AreEqual(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Found your snippets in {0}: 2 snippets, changed {1} at {2}.",
                    WorkAccount,
                    changed.ToString("M", CultureInfo.CurrentCulture),
                    changed.ToString("t", CultureInfo.CurrentCulture)),
                model.Sync.FoundText);
            Assert.AreEqual("Location: OneDrive - Contoso \u203A PowerToys \u203A TextExpander \u203A snippets.txt", model.Sync.FoundLocationText);
            Assert.AreEqual(WorkFile, model.Sync.FoundFilePath, "The tooltip has the full path.");
            Assert.AreEqual(string.Empty, model.SnippetsPath, "Looking must not switch anything.");

            Assert.IsNull(model.Sync.UseFoundSnippets());

            Assert.AreEqual(StandardSetting, model.SnippetsPath);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.AreEqual("Text Expander now uses the snippets in " + WorkAccount + ".", announced);
            CollectionAssert.AreEqual(local, File.ReadAllBytes(LocalFile));
            Assert.AreEqual(0, environment.Watches.Count);
        }

        [TestMethod]
        public void AnotherPcWaitsAndSwitchesWhenOneDriveDeliversTheFile()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;
            StartSetup(model);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsTrue(model.Sync.IsWaiting);
            Assert.IsTrue(model.Sync.IsSyncOn, "The toggle stays On while waiting.");
            Assert.IsFalse(model.Sync.IsFound);
            FakeFileWatch watch = environment.Watches.Single();
            Assert.AreEqual(WorkFolder, watch.Folder);
            CollectionAssert.AreEqual(BothFileNames, watch.FileNames.ToArray(), "Either name counts.");
            Assert.IsFalse(Directory.Exists(WorkFolder), "Waiting must not create the OneDrive folder.");

            // Collapsing the box keeps waiting, and showing the state starts nothing new.
            model.Sync.LeaveSetup(pageClosing: false);
            Assert.IsTrue(model.Sync.IsWaiting);
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.AreSame(watch, environment.Watches.Single());

            watch.Trigger();
            Assert.IsTrue(model.Sync.IsWaiting, "Nothing has arrived yet.");
            Assert.AreEqual(string.Empty, model.SnippetsPath, "This PC's snippets stay in use while waiting.");

            WriteFile(WorkFile, "!a=1\n");
            watch.Trigger();

            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.IsTrue(watch.Disposed);
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsTrue(model.Sync.IsSyncOn);
            Assert.IsFalse(model.Sync.IsSetupVisible);
            Assert.AreEqual("Found your snippets in " + WorkAccount + ". Text Expander now uses them.", announced);
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.AreEqual(1, environment.Watches.Count, "Nothing starts watching again.");
        }

        [TestMethod]
        public void CancelStopsWaitingAndKeepsThisPcsSnippets()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;
            StartSetup(model);
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            FakeFileWatch watch = environment.Watches.Single();

            model.Sync.CancelWaiting();

            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.IsTrue(watch.Disposed);
            Assert.IsFalse(model.Sync.IsSyncOn, "Cancel turns the toggle back off.");
            Assert.IsFalse(model.Sync.IsSetupVisible);
            Assert.AreEqual(TextExpanderSyncViewModel.FirstPcIndex, model.Sync.SetupKindIndex, "Turning it on again starts afresh.");
            Assert.AreEqual("Stopped looking for your snippets.", announced);
            Assert.IsFalse(model.Sync.ShowDetails, "Announced, but nothing is left under the toggle.");

            WriteFile(WorkFile, "!a=1\n");
            watch.Trigger();
            Assert.AreEqual(string.Empty, model.SnippetsPath, "A late callback must not switch after Cancel.");
            Assert.AreEqual(LocalFile, model.SnippetsFilePath);
        }

        [TestMethod]
        public async Task AFolderChosenInsideOneDriveIsSavedWithItsAccountVariable()
        {
            WriteFile(LocalFile, LocalSnippets);
            string custom = Path.Combine(_work, "Shared Notes", "powertoys-textexpander");
            WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n");
            string empty = Path.Combine(_work, "Not synced yet");
            Directory.CreateDirectory(empty);
            string next = custom;
            var starts = new System.Collections.Generic.List<string>();
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment, pickFolder: start =>
            {
                starts.Add(start);
                return Task.FromResult(next);
            });
            StartSetup(model);

            Assert.IsNull(await model.Sync.ChooseDifferentFolderAsync());
            Assert.IsTrue(model.Sync.IsAnotherPcSelected);
            Assert.IsTrue(model.Sync.IsFound);

            // A chosen folder without a snippet file is watched like the standard one.
            next = empty;
            Assert.IsNull(await model.Sync.ChooseDifferentFolderAsync());
            Assert.IsTrue(model.Sync.IsWaiting);
            Assert.AreEqual(empty, environment.Watches.Single().Folder);

            next = custom;
            Assert.IsNull(await model.Sync.ChooseDifferentFolderAsync());
            Assert.IsTrue(environment.Watches.Single().Disposed);
            Assert.IsNull(model.Sync.UseFoundSnippets());

            Assert.AreEqual(@"%OneDriveCommercial%\Shared Notes\powertoys-textexpander", model.SnippetsPath);
            Assert.AreEqual(Path.Combine(custom, "snippets.txt"), model.SnippetsFilePath);
            CollectionAssert.AreEqual(new[] { _work, _work, _work }, starts, "The picker opens at the selected account's folder.");
        }

        [TestMethod]
        public async Task CancellingThePickerChangesNothing()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment, pickFolder: _ => Task.FromResult<string>(null));
            StartSetup(model);

            Assert.IsNull(await model.Sync.ChooseDifferentFolderAsync());

            Assert.IsTrue(model.Sync.IsFirstPcSelected);
            Assert.IsFalse(model.Sync.HasSyncError);
            Assert.AreEqual(string.Empty, model.SnippetsPath);
        }

        [TestMethod]
        public async Task AFolderOutsideOneDriveIsRejected()
        {
            WriteFile(LocalFile, LocalSnippets);
            string outside = Path.Combine(_root, "Dropbox", "Snippets");
            WriteFile(Path.Combine(outside, "snippets.txt"), "!a=1\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment, pickFolder: _ => Task.FromResult(outside));
            StartSetup(model);

            Assert.AreEqual("TextExpander_Sync_FolderOutsideOneDrive", await model.Sync.ChooseDifferentFolderAsync());
            Assert.IsTrue(model.Sync.HasSyncError);
            Assert.IsFalse(model.Sync.IsFound);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.AreEqual(0, environment.Watches.Count);

            // A sibling whose name merely starts with the account folder's name is outside it too.
            Assert.AreEqual("TextExpander_Sync_FolderOutsideOneDrive", model.Sync.ApplyFolderChoice(_work + " (old)"));
            Assert.AreEqual(string.Empty, model.SnippetsPath);
            Assert.AreEqual(0, environment.Writes);
        }

        [TestMethod]
        public void TwoAccountsOfferAChoiceAndSaveTheMatchingVariable()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(personal: true, work: true, generic: _personal);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);

            Assert.IsTrue(model.Sync.HasMultipleAccounts);
            CollectionAssert.AreEqual(TwoAccountNames, model.Sync.Accounts.Select(account => account.DisplayName).ToArray());
            CollectionAssert.AreEqual(TwoAccountVariables, model.Sync.Accounts.Select(account => account.VariablePath).ToArray());

            model.Sync.SelectedAccountIndex = 1;
            Assert.IsNull(model.Sync.CopyToOneDrive());

            Assert.AreEqual(StandardSetting, model.SnippetsPath);
            Assert.IsTrue(File.Exists(WorkNewFile));
            Assert.IsFalse(Directory.Exists(Path.Combine(_personal, "PowerToys")));
        }

        [TestMethod]
        public void TheGenericVariableIsUsedOnlyWithoutASpecificAccount()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(generic: _personal);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);

            TextExpanderSyncAccount account = model.Sync.Accounts.Single();
            Assert.AreEqual("%OneDrive%", account.VariablePath);
            Assert.AreEqual("OneDrive", account.DisplayName);

            Assert.IsNull(model.Sync.CopyToOneDrive());
            Assert.AreEqual(@"%OneDrive%\PowerToys\TextExpander", model.SnippetsPath);
        }

        [TestMethod]
        public void AnExistingOneDriveFolderIsShownAsSyncedWithoutChangingIt()
        {
            // Like a PC where %OneDrive% and %OneDriveCommercial% name the same folder and the
            // snippet folder was typed as an absolute path.
            string custom = Path.Combine(_work, "Shared Notes", "powertoys-textexpander");
            WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n!b=2\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true, generic: _work);
            TextExpanderViewModel model = CreateViewModel(environment, snippetsPath: custom);

            model.Sync.Refresh();

            Assert.AreEqual(1, model.Sync.Accounts.Count);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsTrue(model.Sync.IsSyncOn, "A folder of your own inside OneDrive shows the toggle On.");
            Assert.IsTrue(model.Sync.CanToggle);
            Assert.IsFalse(model.Sync.IsSetupVisible);
            Assert.IsFalse(model.Sync.ShowDetails, "Nothing needs attention, so no empty row shows under the toggle.");
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.AreEqual("Syncing with " + WorkAccount + " \u00B7 2 snippets", model.Sync.SyncedStatusText);
            Assert.AreEqual(Path.Combine(custom, "snippets.txt"), model.Sync.SyncedFilePath);
            Assert.AreEqual(Path.Combine(custom, "snippets.txt"), model.SnippetsFilePath, "A file with the earlier name keeps working.");
            Assert.IsFalse(File.Exists(Path.Combine(custom, "PowerToysTextExpanderSnippets.txt")), "Nothing is renamed or created next to it.");
            Assert.IsFalse(model.Sync.IsOnlineOnly);
            Assert.AreEqual(custom, model.SnippetsPath, "Showing the state must not rewrite the saved folder.");
            Assert.AreEqual(0, environment.Writes);
            Assert.AreEqual(0, environment.Watches.Count);
        }

        [TestMethod]
        public void StopSyncingCopiesOneDriveBackKeepsBothOriginalsAndCanSyncAgain()
        {
            byte[] oldLocal = WriteFile(LocalFile, "!old=local\n");
            string custom = Path.Combine(_work, "Snippets");
            string oneDriveFile = Path.Combine(custom, "snippets.txt");
            byte[] oneDrive = WriteFile(oneDriveFile, "!a=1\n!b=2\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;
            model.Sync.Refresh();
            Assert.IsTrue(model.Sync.IsSynced);

            // Turning the toggle off asks first; the page runs StopSyncing once the user confirms.
            Assert.AreEqual(TextExpanderSyncToggleAction.ConfirmStop, model.Sync.UserToggled(false));
            Assert.IsTrue(model.Sync.IsSyncOn);
            Assert.IsNull(model.Sync.StopSyncing());

            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(LocalNewFile), "This PC's copy gets the current name.");
            CollectionAssert.AreEqual(oldLocal, File.ReadAllBytes(Path.Combine(_local, "snippets.backup.txt")), "The earlier file is kept as a backup named after it.");
            Assert.IsFalse(File.Exists(LocalFile));
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(oneDriveFile), "The OneDrive copy is never changed or deleted.");
            Assert.AreEqual(string.Empty, model.SnippetsPath);
            Assert.AreEqual(LocalNewFile, model.SnippetsFilePath);
            Assert.IsFalse(model.Sync.IsSynced);
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsFalse(model.Sync.IsSetupVisible, "Stopping doesn't open the setup.");
            Assert.AreEqual("Stopped syncing. This PC now uses its own copy of your snippets.", announced);
            Assert.IsFalse(model.Sync.ShowDetails, "Announced, but nothing is left under the toggle.");
            Assert.IsFalse(model.IsFolderRowVisible, "Off with the PowerToys folder: only the toggle shows.");
            Assert.AreEqual(@"%OneDriveCommercial%\Snippets", model.LastSyncFolder, "The OneDrive folder is remembered.");
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(false), "Showing Off asks nothing again.");

            // And back again, as on another PC.
            Assert.AreEqual(TextExpanderSyncToggleAction.ShowSetup, model.Sync.UserToggled(true));
            Assert.IsNull(model.Sync.ApplyFolderChoice(custom));
            Assert.IsTrue(model.Sync.IsFound);
            Assert.IsNull(model.Sync.UseFoundSnippets());
            Assert.AreEqual(@"%OneDriveCommercial%\Snippets", model.SnippetsPath);
            Assert.IsTrue(model.Sync.IsSynced);
        }

        [TestMethod]
        public void StopSyncingKeepsOneDriveWhenItsFileDisappeared()
        {
            byte[] oldLocal = WriteFile(LocalFile, "!old=local\n");
            string custom = Path.Combine(_work, "Snippets");
            string oneDriveFile = Path.Combine(custom, "snippets.txt");
            WriteFile(oneDriveFile, "!a=1\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;
            model.Sync.Refresh();
            Assert.IsTrue(model.Sync.IsSynced);

            File.Delete(oneDriveFile);
            const string missing = "OneDrive's snippet file is no longer available. Text Expander is still using OneDrive.";
            Assert.AreEqual(missing, model.Sync.StopSyncing());
            Assert.AreEqual(missing, model.Sync.SyncError);
            Assert.AreEqual(custom, model.SnippetsPath);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsTrue(model.Sync.IsSyncOn);
            CollectionAssert.AreEqual(oldLocal, File.ReadAllBytes(LocalFile));
            Assert.IsFalse(File.Exists(LocalNewFile));
            Assert.IsNull(announced);
        }

        [TestMethod]
        public void OnlineOnlyMeansCloudDataThatIsNotPinned()
        {
            FileAttributes recall = TextExpanderSyncViewModel.RecallOnDataAccessAttribute;
            FileAttributes pinned = TextExpanderSyncViewModel.PinnedAttribute;
            FileAttributes unpinned = TextExpanderSyncViewModel.UnpinnedAttribute;

            Assert.IsTrue(TextExpanderSyncViewModel.IsOnlineOnlyFile(FileAttributes.Archive | FileAttributes.ReparsePoint | recall));
            Assert.IsTrue(TextExpanderSyncViewModel.IsOnlineOnlyFile(FileAttributes.Offline));
            Assert.IsFalse(TextExpanderSyncViewModel.IsOnlineOnlyFile(FileAttributes.Archive | FileAttributes.ReparsePoint));
            Assert.IsFalse(TextExpanderSyncViewModel.IsOnlineOnlyFile(recall | pinned));

            Assert.AreEqual(FileAttributes.Archive | pinned, TextExpanderSyncViewModel.WithPinState(FileAttributes.Archive | FileAttributes.ReparsePoint | recall | unpinned, keepOnThisPc: true));
            Assert.AreEqual(FileAttributes.Archive, TextExpanderSyncViewModel.WithPinState(FileAttributes.Archive | pinned, keepOnThisPc: false));
            Assert.AreEqual(FileAttributes.Normal, TextExpanderSyncViewModel.WithPinState(pinned, keepOnThisPc: false));
        }

        [TestMethod]
        public void AnOnlineOnlyFileCanBeKeptOnThisPc()
        {
            string custom = Path.Combine(_work, "Snippets");
            string file = Path.Combine(custom, "snippets.txt");
            WriteFile(file, "!a=1\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            environment.AttributeOverrides[file] = FileAttributes.Archive | FileAttributes.ReparsePoint | TextExpanderSyncViewModel.RecallOnDataAccessAttribute;
            TextExpanderViewModel model = CreateViewModel(environment, snippetsPath: custom);
            string announced = null;
            model.Sync.Announcement += (_, message) => announced = message;

            model.Sync.Refresh();
            Assert.IsTrue(model.Sync.IsOnlineOnly);
            Assert.IsTrue(model.Sync.ShowKeepCopyOption);
            Assert.IsFalse(model.Sync.KeepCopyOnThisPc);

            model.Sync.KeepCopyOnThisPc = true;

            Assert.AreEqual(FileAttributes.Archive | TextExpanderSyncViewModel.PinnedAttribute, environment.SetAttributeCalls.Single());
            Assert.IsFalse(model.Sync.IsOnlineOnly);
            Assert.IsTrue(model.Sync.KeepCopyOnThisPc);
            Assert.IsTrue(model.Sync.ShowKeepCopyOption, "The checkbox stays so the choice can be undone.");
            Assert.AreEqual("A copy of your snippets will be kept on this PC.", announced);
        }

        [TestMethod]
        public void MissingOneDriveDisablesTheToggleAndSaysWhy()
        {
            WriteFile(LocalFile, LocalSnippets);
            string app = Path.Combine(_root, "Apps", "OneDrive.exe");
            WriteFile(app, string.Empty);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment();
            environment.Variables["OneDrive"] = Path.Combine(_root, "Profile", "Signed out");
            environment.OneDriveExecutable = app;
            TextExpanderViewModel model = CreateViewModel(environment);

            model.Sync.Refresh();

            Assert.IsTrue(model.Sync.IsOneDriveMissing);
            Assert.IsFalse(model.Sync.CanToggle);
            Assert.IsTrue(model.Sync.ShowDetails, "The OneDrive message shows under the disabled toggle.");
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsFalse(model.Sync.IsSetupVisible);
            Assert.IsFalse(model.Sync.AreStepsEnabled);
            Assert.AreEqual(0, model.Sync.Accounts.Count);
            Assert.IsFalse(model.Sync.HasMultipleAccounts);
            Assert.IsFalse(model.Sync.CanCopy);
            Assert.IsTrue(model.Sync.CanOpenOneDrive);
            Assert.AreEqual("TextExpander_Sync_NoAccount", model.Sync.CopyToOneDrive());

            Assert.IsNull(model.Sync.OpenOneDrive());
            Assert.AreEqual(app, environment.Started.Single());
            Assert.AreEqual(0, environment.Writes);

            environment.OneDriveExecutable = null;
            model.Sync.Refresh();
            Assert.IsFalse(model.Sync.CanOpenOneDrive, "Open OneDrive is hidden when the app isn't installed.");
        }

        [TestMethod]
        public async Task ElevatedSettingsNeverTouchesOneDriveOrTheFileSystem()
        {
            const string Denied = "TextExpander_ElevatedSnippetAccessDenied";
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _work;
            var model = new TextExpanderViewModel(
                CreateSettingsUtils(settings),
                CreateRepository(),
                _ => 0,
                _ => throw new AssertFailedException("The folder picker must not open."),
                isElevated: true,
                Resource,
                new ThrowingTextExpanderSyncEnvironment());

            model.Sync.Refresh();
            model.Sync.RefreshStatus();
            Assert.AreEqual("Not available while PowerToys runs as administrator", model.SnippetFileSummary);
            Assert.AreEqual(Denied, model.Sync.CopyToOneDrive());
            Assert.AreEqual(Denied, model.Sync.UseFoundSnippets());
            Assert.AreEqual(Denied, model.Sync.StopSyncing());
            Assert.AreEqual(Denied, model.Sync.ApplyFolderChoice(_work));
            Assert.AreEqual(Denied, await model.Sync.ChooseDifferentFolderAsync());
            Assert.AreEqual(Denied, model.Sync.OpenOneDrive());
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            model.Sync.SelectedAccountIndex = 0;
            model.Sync.ConflictChoiceIndex = TextExpanderSyncViewModel.ReplaceOneDriveChoice;
            model.Sync.KeepCopyOnThisPc = true;
            model.Sync.CancelWaiting();
            model.Sync.CheckWaitingTarget();
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(false));
            model.Sync.CancelSetup();
            model.Sync.LeaveSetup(pageClosing: false);
            model.Sync.LeaveSetup(pageClosing: true);

            Assert.IsFalse(model.Sync.CanToggle);
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsFalse(model.Sync.ShowDetails);
            Assert.AreEqual(0, model.Sync.Accounts.Count);
            Assert.IsFalse(model.Sync.IsSynced);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.IsFalse(model.Sync.IsFound);
            Assert.IsFalse(model.Sync.KeepCopyOnThisPc);
            Assert.AreEqual(string.Empty, model.SnippetsFilePath);
            Assert.AreEqual(string.Empty, model.DefaultSnippetsFolder);

            // The box can't be opened, so it shows no Folder row, and nothing is remembered.
            Assert.IsFalse(model.IsFolderRowVisible);
            model.LastSyncFolder = _work;
            Assert.AreEqual(string.Empty, model.LastSyncFolder);
        }

        [TestMethod]
        public void TheEmptyFolderBoxShowsTheFolderInUseAndItsTooltipTheFile()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));

            Assert.AreEqual(string.Empty, model.SnippetsPath);
            Assert.AreEqual(_local, model.DefaultSnippetsFolder);
            Assert.AreEqual(LocalFile, model.SnippetsFilePath);

            string custom = Path.Combine(_root, "Dropbox");
            model.SnippetsPath = custom;
            Assert.AreEqual(Path.Combine(custom, "PowerToysTextExpanderSnippets.txt"), model.SnippetsFilePath, "An empty folder gets the current name.");
            Assert.AreEqual(_local, model.DefaultSnippetsFolder, "The placeholder still names the folder an empty box would use.");
        }

        [TestMethod]
        public void SummaryDescribesTheDefaultFolderInSingularAndPlural()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));
            model.Sync.RefreshStatus();

            Assert.AreEqual("Saved on this PC \u00B7 3 snippets", model.SnippetFileSummary);

            WriteFile(LocalFile, "!only=one\n");
            model.LoadSnippets();
            Assert.AreEqual("Saved on this PC \u00B7 1 snippet", model.SnippetFileSummary);
        }

        [TestMethod]
        public void SummaryDescribesAFolderYouChose()
        {
            string custom = Path.Combine(_root, "Dropbox", "Snippets");
            string file = Path.Combine(custom, "snippets.txt");
            WriteFile(file, "!a=1\n!b=2\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            model.Sync.RefreshStatus();

            Assert.IsFalse(model.Sync.IsSynced);
            Assert.AreEqual("Saved in a folder you chose \u00B7 2 snippets", model.SnippetFileSummary);

            WriteFile(file, "!a=1\n");
            model.LoadSnippets();
            Assert.AreEqual("Saved in a folder you chose \u00B7 1 snippet", model.SnippetFileSummary);
        }

        [TestMethod]
        public void SummaryNamesTheOneDriveAccountAndFollowsSyncChanges()
        {
            string custom = Path.Combine(_work, "Snippets");
            string file = Path.Combine(custom, "snippets.txt");
            WriteFile(file, "!a=1\n!b=2\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            var changed = new System.Collections.Generic.List<string>();
            model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            Assert.AreEqual("Saved in a folder you chose \u00B7 2 snippets", model.SnippetFileSummary, "Before OneDrive has been checked.");

            model.Sync.RefreshStatus();

            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.SnippetFileSummary));
            Assert.AreEqual("Syncing with " + WorkAccount + " \u00B7 2 snippets", model.SnippetFileSummary);

            WriteFile(file, "!a=1\n");
            model.LoadSnippets();
            Assert.AreEqual("Syncing with " + WorkAccount + " \u00B7 1 snippet", model.SnippetFileSummary);

            Assert.IsNull(model.Sync.StopSyncing());
            Assert.AreEqual("Saved on this PC \u00B7 1 snippet", model.SnippetFileSummary);
        }

        [TestMethod]
        public void SummaryLeavesOutTheCountWhenTheFileCantBeRead()
        {
            string custom = Path.Combine(_root, "Dropbox", "Snippets");
            string file = Path.Combine(custom, "snippets.txt");
            WriteFile(file, "!a=1\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            using (var locked = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                model.LoadSnippets();
                Assert.AreEqual("Saved in a folder you chose", model.SnippetFileSummary);
            }

            string synced = Path.Combine(_work, "Snippets");
            string syncedFile = Path.Combine(synced, "snippets.txt");
            WriteFile(syncedFile, "!a=1\n");
            model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: synced);
            model.Sync.RefreshStatus();
            using (var locked = new FileStream(syncedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                model.LoadSnippets();
                Assert.AreEqual("Syncing with " + WorkAccount, model.SnippetFileSummary);
            }
        }

        [TestMethod]
        public void TheStatusShownOnThePageReadsNoOneDriveFileContent()
        {
            // Reading an online-only file makes OneDrive download it, so only opening the setup may.
            WriteFile(LocalFile, LocalSnippets);
            WriteFile(WorkFile, "!a=1\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);

            model.Sync.RefreshStatus();

            Assert.AreEqual(0, environment.Reads.Count);
            Assert.IsFalse(model.Sync.HasConflict);
            Assert.AreEqual(0, environment.Writes);

            // Opening the box refreshes too, still without reading OneDrive files.
            model.Sync.Refresh();
            Assert.AreEqual(0, environment.Reads.Count);
            Assert.IsFalse(model.Sync.HasConflict);

            StartSetup(model);
            Assert.IsTrue(model.Sync.HasConflict);
            CollectionAssert.Contains(environment.Reads, WorkFile);
        }

        [TestMethod]
        public void TurningOnShowsTheSetupAndAnUnfinishedSetupTurnsItOff()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            model.Sync.RefreshStatus();
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsTrue(model.Sync.CanToggle);
            Assert.IsFalse(model.Sync.IsSetupVisible, "The setup stays hidden until the toggle is turned on.");
            Assert.IsFalse(model.Sync.ShowDetails);

            // Turning it off again cancels the setup.
            StartSetup(model);
            Assert.IsTrue(model.Sync.ShowDetails);
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(false));
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsFalse(model.Sync.IsSetupVisible);

            // So does collapsing the box, or leaving the page.
            StartSetup(model);
            model.Sync.LeaveSetup(pageClosing: false);
            Assert.IsFalse(model.Sync.IsSyncOn);

            StartSetup(model);
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsWaiting);
            model.Sync.LeaveSetup(pageClosing: true);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.IsTrue(environment.Watches.Single().Disposed);
            Assert.IsFalse(model.Sync.IsSyncOn);

            // Showing Off starts nothing, and nothing was written or switched.
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(false));
            Assert.AreEqual(1, environment.Watches.Count);
            Assert.AreEqual(0, environment.Writes);
            Assert.AreEqual(string.Empty, model.SnippetsPath);
        }

        [TestMethod]
        public void TurningOffAsksFirstAndCancellingKeepsSyncing()
        {
            string custom = Path.Combine(_work, "Shared Notes", "powertoys-textexpander");
            byte[] oneDrive = WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n");
            byte[] local = WriteFile(LocalFile, "!local=1\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment, snippetsPath: custom);
            model.Sync.RefreshStatus();
            Assert.IsTrue(model.Sync.IsSyncOn);

            Assert.AreEqual(TextExpanderSyncToggleAction.ConfirmStop, model.Sync.UserToggled(false));

            // The user chose Cancel, so the page puts the toggle back On; that starts nothing.
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(true));
            Assert.IsTrue(model.Sync.IsSyncOn);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsFalse(model.Sync.IsSetupVisible);
            Assert.AreEqual(custom, model.SnippetsPath);
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(Path.Combine(custom, "snippets.txt")));
            CollectionAssert.AreEqual(local, File.ReadAllBytes(LocalFile));
            Assert.AreEqual(0, environment.Writes);
        }

        [TestMethod]
        public void SettingsPicksTheFileInAFolderTheWayTheEngineDoes()
        {
            string custom = Path.Combine(_root, "Chosen");
            foreach ((bool hasNew, bool hasLegacy, string expected) in new[]
            {
                (false, false, "PowerToysTextExpanderSnippets.txt"),
                (false, true, "snippets.txt"),
                (true, false, "PowerToysTextExpanderSnippets.txt"),
                (true, true, "PowerToysTextExpanderSnippets.txt"),
            })
            {
                foreach (string folder in new[] { _local, custom })
                {
                    Directory.CreateDirectory(folder);
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        File.Delete(file);
                    }

                    if (hasNew)
                    {
                        WriteFile(Path.Combine(folder, "PowerToysTextExpanderSnippets.txt"), "!new=1\n");
                    }

                    if (hasLegacy)
                    {
                        WriteFile(Path.Combine(folder, "snippets.txt"), "!old=1\n");
                    }

                    TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: folder == _local ? string.Empty : folder);
                    Assert.AreEqual(Path.Combine(folder, expected), model.SnippetsFilePath, $"new={hasNew}, legacy={hasLegacy}, folder={folder}");
                }
            }
        }

        [TestMethod]
        public void AConflictWithTheCurrentNameKeepsThatNameForTheBackupAndThisPcsCopy()
        {
            byte[] local = WriteFile(LocalFile, LocalSnippets);
            byte[] oneDrive = WriteFile(WorkNewFile, "!a=1\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));
            StartSetup(model);
            Assert.AreEqual("Use OneDrive's file, and save this PC's as PowerToysTextExpanderSnippets (TESTPC).txt.", model.Sync.KeepBothDescription);
            model.Sync.ConflictChoiceIndex = TextExpanderSyncViewModel.ReplaceOneDriveChoice;

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(local, File.ReadAllBytes(WorkNewFile));
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(Path.Combine(WorkFolder, "PowerToysTextExpanderSnippets.backup.txt")));
            Assert.IsFalse(File.Exists(WorkFile));

            // Keep both, from scratch.
            File.Delete(Path.Combine(WorkFolder, "PowerToysTextExpanderSnippets.backup.txt"));
            File.WriteAllBytes(WorkNewFile, oneDrive);
            model = CreateViewModel(CreateEnvironment(work: true));
            StartSetup(model);
            model.Sync.ConflictChoiceIndex = TextExpanderSyncViewModel.KeepBothChoice;

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(WorkNewFile));
            CollectionAssert.AreEqual(local, File.ReadAllBytes(Path.Combine(WorkFolder, "PowerToysTextExpanderSnippets (TESTPC).txt")));
            Assert.AreEqual(WorkNewFile, model.SnippetsFilePath);
        }

        [TestMethod]
        public void ALegacyOneDriveFileIsReplacedInPlaceSoEarlierVersionsStillFindIt()
        {
            byte[] local = WriteFile(LocalNewFile, LocalSnippets);
            byte[] oneDrive = WriteFile(WorkFile, "!a=1\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));
            StartSetup(model);
            Assert.IsTrue(model.Sync.HasConflict, "A file with the earlier name counts as OneDrive's snippets.");
            model.Sync.ConflictChoiceIndex = TextExpanderSyncViewModel.ReplaceOneDriveChoice;

            Assert.IsNull(model.Sync.CopyToOneDrive());

            CollectionAssert.AreEqual(local, File.ReadAllBytes(WorkFile));
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(Path.Combine(WorkFolder, "snippets.backup.txt")));
            Assert.IsFalse(File.Exists(WorkNewFile), "Never renamed.");
            Assert.AreEqual(WorkFile, model.SnippetsFilePath);
        }

        [TestMethod]
        public void AnotherPcFindsAndWaitsForEitherName()
        {
            WriteFile(LocalFile, LocalSnippets);
            WriteFile(WorkNewFile, "!a=1\n!b=2\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsTrue(model.Sync.IsFound);
            Assert.IsNull(model.Sync.UseFoundSnippets());
            Assert.AreEqual(WorkNewFile, model.SnippetsFilePath);

            // Waiting, then the file arrives under the current name.
            File.Delete(WorkNewFile);
            environment = CreateEnvironment(work: true);
            model = CreateViewModel(environment);
            StartSetup(model);
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsWaiting);

            WriteFile(WorkNewFile, "!a=1\n");
            environment.Watches.Single().Trigger();

            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
            Assert.AreEqual(WorkNewFile, model.SnippetsFilePath);
        }

        [TestMethod]
        public void ConflictChoicesNameTheSnippetsForOneOrMany()
        {
            // QA-19: "Use the 1 in OneDrive" and "Replace them with this PC's 1" named nothing.
            WriteFile(LocalFile, "!only=this PC\n");
            WriteFile(WorkFile, "!one=OneDrive\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));

            StartSetup(model);
            Assert.IsTrue(model.Sync.HasConflict);
            Assert.AreEqual("OneDrive already has 1 snippet.", model.Sync.ConflictTitle);
            Assert.AreEqual("Use the 1 snippet in OneDrive", model.Sync.UseOneDriveChoiceText);
            Assert.AreEqual("Replace with this PC's 1 snippet", model.Sync.ReplaceChoiceText);
        }

        [TestMethod]
        public void TheStopSyncingQuestionNamesTheBackupItWillMake()
        {
            string custom = Path.Combine(_work, "Snippets");
            WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n");
            WriteFile(LocalNewFile, "!old=local\n");
            WriteFile(Path.Combine(_local, "PowerToysTextExpanderSnippets.backup.txt"), "!older=backup\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            model.Sync.Refresh();

            // QA-19: says where this PC's snippets go, by the name StopSyncing then uses.
            Assert.AreEqual("Copies OneDrive's snippets here and keeps this PC's as PowerToysTextExpanderSnippets.backup (2).txt.", model.Sync.DescribeStopSyncing());
            Assert.IsNull(model.Sync.StopSyncing());
            Assert.IsTrue(File.Exists(Path.Combine(_local, "PowerToysTextExpanderSnippets.backup (2).txt")));

            // Nothing of this PC's own to keep.
            foreach (string file in Directory.GetFiles(_local))
            {
                File.Delete(file);
            }

            Assert.AreEqual("Copies OneDrive's snippets here.", model.Sync.DescribeStopSyncing());
        }

        [TestMethod]
        public void StopSyncingBacksUpACurrentNameFileOrWritesAFreshOne()
        {
            string custom = Path.Combine(_work, "Snippets");
            byte[] oneDrive = WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n!b=2\n");
            byte[] oldLocal = WriteFile(LocalNewFile, "!old=local\n");
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            model.Sync.Refresh();

            Assert.IsNull(model.Sync.StopSyncing());

            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(LocalNewFile));
            CollectionAssert.AreEqual(oldLocal, File.ReadAllBytes(Path.Combine(_local, "PowerToysTextExpanderSnippets.backup.txt")));
            Assert.IsFalse(File.Exists(LocalFile));

            // Nothing on this PC yet: the copy simply gets the current name.
            foreach (string file in Directory.GetFiles(_local))
            {
                File.Delete(file);
            }

            model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: custom);
            model.Sync.Refresh();
            Assert.IsNull(model.Sync.StopSyncing());

            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(LocalNewFile));
            Assert.AreEqual(1, Directory.GetFiles(_local).Length, "No backup when there was nothing to replace.");
            Assert.AreEqual(LocalNewFile, model.SnippetsFilePath);
        }

        [TestMethod]
        public void TheFolderRowShowsWhenSyncedOrAFolderIsSetButNeverDuringSetup()
        {
            // The PowerToys folder, not syncing: the box shows only the toggle.
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            var changed = new System.Collections.Generic.List<string>();
            model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            model.Sync.RefreshStatus();
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsFalse(model.IsFolderRowVisible);
            Assert.IsFalse(model.Sync.ShowDetails);

            // During the setup, its own Choose a different folder in OneDrive… is the only folder picker.
            StartSetup(model);
            Assert.IsFalse(model.IsFolderRowVisible);
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsWaiting);
            Assert.IsFalse(model.IsFolderRowVisible, "Not while waiting either.");

            // Synced: shown, so the folder in use can be seen.
            changed.Clear();
            WriteFile(WorkFile, "!a=1\n");
            environment.Watches.Single().Trigger();
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsTrue(model.IsFolderRowVisible);
            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsFolderRowVisible));

            // A folder of the user's own outside OneDrive: sync is off, but the row stays so it can be cleared.
            string dropbox = Path.Combine(_root, "Dropbox", "Snippets");
            model.SnippetsPath = dropbox;
            Assert.IsFalse(model.Sync.IsSyncOn);
            Assert.IsTrue(model.IsFolderRowVisible);

            // Turning sync on hides it for the setup, and cancelling the setup brings it back.
            changed.Clear();
            StartSetup(model);
            Assert.IsFalse(model.IsFolderRowVisible);
            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsFolderRowVisible), "The setup's visibility is passed on.");
            Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(false));
            Assert.IsTrue(model.IsFolderRowVisible);

            // Cleared: the PowerToys folder again, so only the toggle.
            model.SnippetsPath = string.Empty;
            Assert.IsFalse(model.IsFolderRowVisible);

            // A typo stays visible too, though Text Expander falls back to the PowerToys folder.
            model = CreateViewModel(CreateEnvironment(work: true), snippetsPath: "relative\\path");
            Assert.AreEqual(Path.Combine(_local, "snippets.txt"), model.SnippetsFilePath);
            Assert.IsTrue(model.IsFolderRowVisible);
        }

        [TestMethod]
        public void WithoutOneDriveTheFolderRowShowsSoAnotherSyncAppCanBeSet()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment());
            var changed = new System.Collections.Generic.List<string>();
            model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            Assert.IsFalse(model.IsFolderRowVisible, "Unknown until the box has looked for OneDrive.");

            model.Sync.RefreshStatus();

            Assert.IsTrue(model.Sync.IsOneDriveMissing);
            Assert.IsTrue(model.Sync.ShowDetails, "The message saying why the toggle is off still shows.");
            Assert.IsTrue(model.IsFolderRowVisible);
            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsFolderRowVisible));
        }

        [TestMethod]
        public void TheFolderDescriptionNamesTheFileInUse()
        {
            // A file an earlier version made keeps its name, so the description says that name.
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true));
            Assert.AreEqual("Your snippets are saved as snippets.txt in this folder.", model.SnippetsFolderDescription);

            // An empty folder gets the current name, which is what the engine will create.
            var changed = new System.Collections.Generic.List<string>();
            model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            string empty = Path.Combine(_root, "Empty");
            Directory.CreateDirectory(empty);
            model.SnippetsPath = empty;
            Assert.AreEqual("Your snippets are saved as PowerToysTextExpanderSnippets.txt in this folder.", model.SnippetsFolderDescription);
            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.SnippetsFolderDescription), "A new folder updates the description.");

            // With both, the current name wins, as in the engine.
            WriteFile(Path.Combine(empty, "snippets.txt"), "!a=1\n");
            WriteFile(Path.Combine(empty, "PowerToysTextExpanderSnippets.txt"), "!b=2\n");
            model.LoadSnippets();
            Assert.AreEqual("Your snippets are saved as PowerToysTextExpanderSnippets.txt in this folder.", model.SnippetsFolderDescription);
        }

        [TestMethod]
        public void EveryChangeIsAnnouncedAndNoneStaysUnderTheToggle()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            var announced = new System.Collections.Generic.List<string>();
            model.Sync.Announcement += (_, message) => announced.Add(message);
            StartSetup(model);
            Assert.IsNull(model.Sync.CopyToOneDrive());

            // After a sync, the toggle and the Folder row; the summary says the rest.
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsFalse(model.Sync.ShowDetails);
            Assert.IsTrue(model.IsFolderRowVisible);

            // Online-only needs attention, so it shows; keeping a copy is then only announced.
            environment.AttributeOverrides[WorkNewFile] = FileAttributes.Archive | FileAttributes.ReparsePoint | TextExpanderSyncViewModel.RecallOnDataAccessAttribute;
            model.Sync.Refresh();
            Assert.IsTrue(model.Sync.ShowDetails);
            model.Sync.KeepCopyOnThisPc = true;
            Assert.IsTrue(model.Sync.ShowDetails, "The keep-a-copy option stays, so it can be turned back off.");

            Assert.IsNull(model.Sync.StopSyncing());

            CollectionAssert.AreEqual(
                new[]
                {
                    "Your snippets are now syncing with " + WorkAccount + ".",
                    "A copy of your snippets will be kept on this PC.",
                    "Stopped syncing. This PC now uses its own copy of your snippets.",
                },
                announced);
            Assert.IsFalse(model.Sync.ShowDetails, "Off: only the toggle shows.");
        }

        [TestMethod]
        public void TurningSyncBackOnUsesTheOneDriveFolderItLastUsed()
        {
            // A folder in the work account other than the usual one, and a personal account too.
            string custom = Path.Combine(_work, "Shared Notes", "powertoys-textexpander");
            byte[] oneDrive = WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n!b=2\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(personal: true, work: true);
            TextExpanderViewModel model = CreateViewModel(environment, snippetsPath: custom);
            model.Sync.Refresh();
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsNull(model.Sync.StopSyncing());
            Assert.AreEqual(@"%OneDriveCommercial%\Shared Notes\powertoys-textexpander", model.LastSyncFolder);

            // Another PC finds it at once, with nothing to wait for or search.
            StartSetup(model);
            Assert.AreEqual(1, model.Sync.SelectedAccountIndex, "Its account is selected.");
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsFound);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.AreEqual(0, environment.Watches.Count);
            Assert.AreEqual(0, environment.Listed.Count, "Found where it was last synced, so OneDrive isn't searched.");
            StringAssert.StartsWith(model.Sync.FoundText, "Found your snippets in " + WorkAccount + ": 2 snippets");
            model.Sync.CancelSetup();

            // First PC compares against it, with OneDrive's snippets chosen.
            StartSetup(model);
            Assert.IsTrue(model.Sync.IsFirstPcSelected);
            Assert.IsTrue(model.Sync.HasConflict);
            Assert.AreEqual("OneDrive already has 2 snippets.", model.Sync.ConflictTitle);
            Assert.AreEqual(TextExpanderSyncViewModel.UseOneDriveChoice, model.Sync.ConflictChoiceIndex);

            Assert.IsNull(model.Sync.CopyToOneDrive());

            Assert.AreEqual(@"%OneDriveCommercial%\Shared Notes\powertoys-textexpander", model.SnippetsPath);
            Assert.AreEqual(Path.Combine(custom, "snippets.txt"), model.SnippetsFilePath);
            CollectionAssert.AreEqual(oneDrive, File.ReadAllBytes(Path.Combine(custom, "snippets.txt")));
            Assert.IsFalse(Directory.Exists(WorkFolder), "Nothing goes to the standard folder.");
            Assert.IsTrue(model.Sync.IsSynced);
        }

        [TestMethod]
        public void APageOpenedLaterUsesTheRememberedFolderFromTheSavedSettings()
        {
            string custom = Path.Combine(_work, "Snippets");
            WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n!b=2\n");
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = custom;
            var saved = new System.Collections.Generic.List<string>();
            TextExpanderViewModel model = CreateViewModel(CreateEnvironment(work: true), settings, saved);
            model.Sync.Refresh();
            Assert.IsNull(model.Sync.StopSyncing());

            // What Settings saved, read back the way it loads the file.
            StringAssert.Contains(saved.Last(), "\"last_sync_folder\":{\"value\":");
            TextExpanderSettings reloaded = JsonSerializer.Deserialize(saved.Last(), SettingsSerializationContext.Default.TextExpanderSettings);
            Assert.AreEqual(string.Empty, reloaded.Properties.SnippetsPath.Value);
            Assert.AreEqual(@"%OneDriveCommercial%\Snippets", reloaded.Properties.LastSyncFolder.Value);

            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            model = CreateViewModel(environment, reloaded);
            StartSetup(model);
            Assert.IsTrue(model.Sync.HasConflict);
            Assert.AreEqual("OneDrive already has 2 snippets.", model.Sync.ConflictTitle);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsFound);
            Assert.AreEqual(0, environment.Watches.Count);
            Assert.IsNull(model.Sync.UseFoundSnippets());
            Assert.AreEqual(@"%OneDriveCommercial%\Snippets", model.SnippetsPath);

            // An older settings file has no such key, and loads with nothing remembered.
            TextExpanderSettings older = JsonSerializer.Deserialize(
                "{\"name\":\"TextExpander\",\"version\":\"0.0.1\",\"properties\":{\"snippets_path\":{\"value\":\"\"}}}",
                SettingsSerializationContext.Default.TextExpanderSettings);
            Assert.AreEqual(string.Empty, CreateViewModel(CreateEnvironment(work: true), older).LastSyncFolder);
        }

        [TestMethod]
        public void TheStandardFolderIsUsedWhenTheRememberedOneIsGone()
        {
            WriteFile(LocalFile, LocalSnippets);
            string emptied = Path.Combine(_work, "Emptied");
            Directory.CreateDirectory(emptied);
            string outside = Path.Combine(_root, "Dropbox", "Snippets");
            WriteFile(Path.Combine(outside, "snippets.txt"), "!a=1\n");

            foreach (string remembered in new[] { @"%OneDriveCommercial%\Gone", @"%OneDriveCommercial%\Emptied", outside, @"relative\folder" })
            {
                var settings = new TextExpanderSettings();
                settings.Properties.LastSyncFolder.Value = remembered;
                TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
                TextExpanderViewModel model = CreateViewModel(environment, settings);

                StartSetup(model);
                Assert.IsFalse(model.Sync.HasConflict, remembered);
                model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
                Assert.IsTrue(model.Sync.IsWaiting, remembered);
                Assert.AreEqual(WorkFolder, environment.Watches.Single().Folder, remembered);
                model.Sync.CancelWaiting();
            }

            // Choosing a different folder still wins over a remembered one.
            string custom = Path.Combine(_work, "Snippets");
            WriteFile(Path.Combine(custom, "snippets.txt"), "!a=1\n");
            string chosen = Path.Combine(_work, "Chosen");
            WriteFile(Path.Combine(chosen, "PowerToysTextExpanderSnippets.txt"), "!c=3\n");
            var withMemory = new TextExpanderSettings();
            withMemory.Properties.LastSyncFolder.Value = @"%OneDriveCommercial%\Snippets";
            TextExpanderViewModel chooser = CreateViewModel(CreateEnvironment(work: true), withMemory);
            StartSetup(chooser);
            Assert.IsNull(chooser.Sync.ApplyFolderChoice(chosen));
            Assert.IsTrue(chooser.Sync.IsFound);
            Assert.IsNull(chooser.Sync.UseFoundSnippets());
            Assert.AreEqual(@"%OneDriveCommercial%\Chosen", chooser.SnippetsPath);
        }

        [TestMethod]
        public void ClearingOrChangingTheFolderWhileSyncingRemembersTheOneDriveFolder()
        {
            string first = Path.Combine(_work, "First");
            WriteFile(Path.Combine(first, "snippets.txt"), "!a=1\n");
            string second = Path.Combine(_work, "Second");
            WriteFile(Path.Combine(second, "snippets.txt"), "!b=2\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment, snippetsPath: first);
            model.Sync.RefreshStatus();
            Assert.IsTrue(model.Sync.IsSynced);

            // Moving to another OneDrive folder is still syncing, so there's nothing to remember yet.
            model.SnippetsPath = second;
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.AreEqual(string.Empty, model.LastSyncFolder);

            // Clearing the Folder row leaves OneDrive.
            model.SnippetsPath = string.Empty;
            Assert.IsFalse(model.Sync.IsSynced);
            Assert.AreEqual(@"%OneDriveCommercial%\Second", model.LastSyncFolder);

            // So does choosing a folder of another sync app.
            model.SnippetsPath = first;
            Assert.IsTrue(model.Sync.IsSynced);
            model.SnippetsPath = Path.Combine(_root, "Dropbox");
            Assert.IsFalse(model.Sync.IsSynced);
            Assert.AreEqual(@"%OneDriveCommercial%\First", model.LastSyncFolder);
            Assert.AreEqual(0, environment.Writes, "Remembering it changes no snippet file.");
        }

        [TestMethod]
        public void NothingFoundEndsTheSpinnerSaysWhatWasCheckedAndKeepsWatching()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            var announced = new System.Collections.Generic.List<string>();
            model.Sync.Announcement += (_, message) => announced.Add(message);
            StartSetup(model);
            Assert.AreEqual(string.Empty, model.Sync.WaitingLocationText, "Nothing until it has looked.");

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsFalse(model.Sync.IsSearching, "No spinner once the search is over.");
            Assert.IsTrue(model.Sync.IsWaiting);
            Assert.IsFalse(model.Sync.IsFound);
            Assert.IsFalse(model.Sync.HasCandidates);
            CollectionAssert.Contains(environment.Listed, _work, "OneDrive was searched.");
            Assert.AreEqual(Resource("TextExpander_Sync_NotFoundMessage"), model.Sync.NotFoundMessage);
            Assert.AreEqual("Default location: OneDrive - Contoso \u203A PowerToys \u203A TextExpander", model.Sync.WaitingLocationText);
            Assert.AreEqual(WorkNewFile, model.Sync.WaitingFilePath, "The tooltip has the full path.");
            Assert.AreEqual("Couldn't find your snippets in OneDrive.", announced.Single());
            Assert.AreEqual(WorkFolder, environment.Watches.Single().Folder, "It keeps watching where the snippets belong.");
            Assert.IsFalse(Directory.Exists(WorkFolder), "Watching must not create the OneDrive folder.");

            // They arrive after all: it switches by itself, and the not-found state is gone.
            WriteFile(WorkFile, "!a=1\n");
            environment.Watches.Single().Trigger();

            Assert.IsTrue(model.Sync.IsSynced);
            Assert.AreEqual(StandardSetting, model.SnippetsPath);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.AreEqual(string.Empty, model.Sync.NotFoundMessage);
            Assert.AreEqual(string.Empty, model.Sync.WaitingLocationText);
            Assert.AreEqual(string.Empty, model.Sync.WaitingFilePath);
            Assert.AreEqual("Found your snippets in " + WorkAccount + ". Text Expander now uses them.", announced.Last());
        }

        [TestMethod]
        public void AChosenFolderIsNotSearchedBeyond()
        {
            WriteFile(LocalFile, LocalSnippets);
            string chosen = Path.Combine(_work, "Not synced yet");
            Directory.CreateDirectory(chosen);
            WriteFile(Path.Combine(_work, "PowerToys Text Expander", "snippets.txt"), "!a=1\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);

            Assert.IsNull(model.Sync.ApplyFolderChoice(chosen + Path.DirectorySeparatorChar));

            Assert.IsTrue(model.Sync.IsAnotherPcSelected);
            Assert.AreEqual(0, environment.Listed.Count, "The folder the user chose is the answer; nothing else is searched.");
            Assert.IsFalse(model.Sync.IsSearching);
            Assert.IsFalse(model.Sync.HasCandidates);
            Assert.IsTrue(model.Sync.IsWaiting);
            Assert.AreEqual(Resource("TextExpander_Sync_NotFoundChosenMessage"), model.Sync.NotFoundMessage);
            Assert.AreEqual("Location: OneDrive - Contoso \u203A Not synced yet", model.Sync.WaitingLocationText);
            Assert.AreEqual(Path.Combine(chosen, "PowerToysTextExpanderSnippets.txt"), model.Sync.WaitingFilePath);
            Assert.AreEqual(chosen, environment.Watches.Single().Folder);
        }

        [TestMethod]
        public void NothingFoundCanSetThisPcUpAsTheFirstInstead()
        {
            WriteFile(LocalFile, LocalSnippets);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);
            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsWaiting);

            model.Sync.SetUpAsFirstPc();

            Assert.IsTrue(model.Sync.IsFirstPcSelected);
            Assert.IsFalse(model.Sync.IsWaiting);
            Assert.IsTrue(environment.Watches.Single().Disposed);
            Assert.IsTrue(model.Sync.IsSetupVisible, "Still setting up, now as the first PC.");
            Assert.IsTrue(model.Sync.CanCopy);
        }

        [TestMethod]
        public void TheRememberedFolderComesBeforeTheStandardOne()
        {
            string remembered = Path.Combine(_work, "Snippets");
            WriteFile(Path.Combine(remembered, "snippets.txt"), "!a=1\n!b=2\n");
            WriteFile(WorkNewFile, "!c=3\n");
            var settings = new TextExpanderSettings();
            settings.Properties.LastSyncFolder.Value = @"%OneDriveCommercial%\Snippets";
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment, settings);
            StartSetup(model);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsTrue(model.Sync.IsFound);
            Assert.AreEqual(Path.Combine(remembered, "snippets.txt"), model.Sync.FoundFilePath);
            StringAssert.Contains(model.Sync.FoundText, ": 2 snippets,");
            Assert.AreEqual(0, environment.Listed.Count);
        }

        [TestMethod]
        public void ASingleFileFoundShowsWhereItIsDownToItsName()
        {
            WriteFile(LocalFile, LocalSnippets);
            string file = Path.Combine(_work, "Shared Notes", "powertoys-textexpander", "snippets.txt");
            WriteFile(file, "!a=1\n");
            var changed = new DateTime(2026, 10, 18, 0, 2, 0, DateTimeKind.Local);
            File.SetLastWriteTime(file, changed);
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            var announced = new System.Collections.Generic.List<string>();
            model.Sync.Announcement += (_, message) => announced.Add(message);
            StartSetup(model);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsTrue(model.Sync.IsFound);
            Assert.IsFalse(model.Sync.HasCandidates, "One file isn't a list.");
            Assert.AreEqual(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Found your snippets in {0}: 1 snippet, changed {1} at {2}.",
                    WorkAccount,
                    changed.ToString("M", CultureInfo.CurrentCulture),
                    changed.ToString("t", CultureInfo.CurrentCulture)),
                model.Sync.FoundText);
            Assert.AreEqual("Location: OneDrive - Contoso \u203A Shared Notes \u203A powertoys-textexpander \u203A snippets.txt", model.Sync.FoundLocationText);
            Assert.AreEqual(file, model.Sync.FoundFilePath);
            CollectionAssert.AreEqual(new[] { model.Sync.FoundText }, announced, "It shows up after a search, so it's announced.");

            Assert.IsNull(model.Sync.UseFoundSnippets());

            Assert.AreEqual(@"%OneDriveCommercial%\Shared Notes\powertoys-textexpander", model.SnippetsPath);
            Assert.AreEqual(file, model.SnippetsFilePath);
        }

        [TestMethod]
        public void SeveralFilesFoundAreListedNewestFirstAndOnlyThatOneIsRead()
        {
            // A OneDrive with a Text Expander folder, an older copy of an earlier app, and
            // snippets.txt files that aren't Text Expander's.
            WriteFile(LocalFile, LocalSnippets);
            string mine = Path.Combine(_work, "Shared Notes", "powertoys-textexpander", "snippets.txt");
            string parentFile = Path.Combine(_work, "Shared Notes", "snippets.txt");
            string oldApp = Path.Combine(_work, "Desktop", "PC Software", "OldTextExpander-windows-arm64", "snippets.txt");
            WriteFile(mine, "!a=1\n!b=2\n");
            WriteFile(parentFile, "!c=3\n");
            WriteFile(oldApp, "!d=4\n!e=5\n!f=6\n");
            File.SetLastWriteTime(mine, new DateTime(2026, 10, 18, 0, 2, 0, DateTimeKind.Local));
            File.SetLastWriteTime(parentFile, new DateTime(2026, 10, 19, 15, 3, 0, DateTimeKind.Local));
            File.SetLastWriteTime(oldApp, new DateTime(2026, 4, 20, 8, 23, 0, DateTimeKind.Local));
            WriteFile(Path.Combine(_work, "Notes", "snippets.txt"), "!g=7\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            var announced = new System.Collections.Generic.List<string>();
            model.Sync.Announcement += (_, message) => announced.Add(message);
            StartSetup(model);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.IsFalse(model.Sync.IsSearching);
            Assert.IsFalse(model.Sync.IsWaiting, "Something was found, so there is nothing to watch for.");
            Assert.IsFalse(model.Sync.IsFound, "Several files are a list.");
            Assert.AreEqual(0, environment.Watches.Count);
            Assert.AreEqual(_work, environment.Listed.First());

            // "snippets.txt" counts only in a folder named for Text Expander or PowerToys, however new.
            CollectionAssert.AreEqual(new[] { mine, oldApp }, model.Sync.Candidates.Select(candidate => candidate.FilePath).ToArray());
            TextExpanderSyncCandidate newest = model.Sync.Candidates[0];
            string october18 = new DateTime(2026, 10, 18).ToString("M", CultureInfo.CurrentCulture);
            string april20 = new DateTime(2026, 4, 20).ToString("M", CultureInfo.CurrentCulture);
            Assert.AreEqual("powertoys-textexpander", newest.FolderName);
            Assert.AreEqual("OneDrive - Contoso \u203A Shared Notes \u203A powertoys-textexpander \u203A snippets.txt", newest.Location, "Down to the file's name.");
            Assert.AreEqual("2 snippets \u00B7 " + october18, newest.Details);
            Assert.AreEqual(april20, model.Sync.Candidates[1].Details, "Not read, so no count.");
            Assert.AreEqual(
                "powertoys-textexpander, 2 snippets, " + october18 + ", OneDrive - Contoso, Shared Notes, powertoys-textexpander, snippets.txt",
                newest.ToString(),
                "What a screen reader says.");
            CollectionAssert.AreEqual(
                new[] { mine },
                environment.Reads.Where(path => path.StartsWith(_work, StringComparison.OrdinalIgnoreCase)).ToArray(),
                "Only the file offered first is read: reading downloads an online-only file.");
            Assert.AreEqual(0, model.Sync.SelectedCandidateIndex, "The newest is selected.");
            Assert.IsTrue(model.Sync.CanUseCandidate);
            Assert.AreEqual("Found 2 snippet files in OneDrive.", announced.Single());
            Assert.AreEqual(string.Empty, model.SnippetsPath, "Searching switches nothing.");

            Assert.IsNull(model.Sync.UseSelectedCandidate());

            Assert.AreEqual(@"%OneDriveCommercial%\Shared Notes\powertoys-textexpander", model.SnippetsPath);
            Assert.AreEqual(mine, model.SnippetsFilePath);
            Assert.IsTrue(model.Sync.IsSynced);
            Assert.IsFalse(model.Sync.HasCandidates);
            Assert.AreEqual(0, environment.Writes, "Using a file found in OneDrive writes nothing.");
        }

        [TestMethod]
        public void TheSelectedAccountIsSearchedFirstAndTheCurrentNameHidesALegacyTwin()
        {
            WriteFile(LocalFile, LocalSnippets);
            string current = Path.Combine(_personal, "Old stuff", "PowerToysTextExpanderSnippets.txt");
            string twin = Path.Combine(_personal, "Old stuff", "snippets.txt");
            string textExpander = Path.Combine(_work, "Text Expander", "snippets.txt");
            WriteFile(current, "!a=1\n");
            WriteFile(twin, "!b=2\n");
            WriteFile(textExpander, "!c=3\n");
            File.SetLastWriteTime(current, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local));
            File.SetLastWriteTime(twin, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local));
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(personal: true, work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);
            Assert.AreEqual(0, model.Sync.SelectedAccountIndex);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;

            Assert.AreEqual(_personal, environment.Listed[0]);
            int firstWork = environment.Listed.FindIndex(folder => folder.StartsWith(_work, StringComparison.OrdinalIgnoreCase));
            int lastPersonal = environment.Listed.FindLastIndex(folder => !folder.StartsWith(_work, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(firstWork > lastPersonal, "Every folder of the selected account comes before the other account's.");

            // Newest first. A folder with both names offers the current one, as the engine reads it.
            CollectionAssert.AreEqual(new[] { textExpander, current }, model.Sync.Candidates.Select(candidate => candidate.FilePath).ToArray());

            model.Sync.SelectedCandidateIndex = 1;
            Assert.IsNull(model.Sync.UseSelectedCandidate());

            Assert.AreEqual(@"%OneDriveConsumer%\Old stuff", model.SnippetsPath, "Saved with the account the file is in.");
            Assert.AreEqual(current, model.SnippetsFilePath);
        }

        [TestMethod]
        public void IfTheNewestFileCantBeReadTheNextNewestIsOfferedInstead()
        {
            WriteFile(LocalFile, LocalSnippets);
            string locked = Path.Combine(_work, "TextExpander new", "PowerToysTextExpanderSnippets.txt");
            string older = Path.Combine(_work, "TextExpander old", "PowerToysTextExpanderSnippets.txt");
            WriteFile(locked, "!a=1\n");
            WriteFile(older, "!b=2\n!c=3\n");
            File.SetLastWriteTime(older, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local));
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            StartSetup(model);

            using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            }

            Assert.IsTrue(model.Sync.IsFound, "The only file left to offer.");
            Assert.AreEqual(older, model.Sync.FoundFilePath);
            StringAssert.Contains(model.Sync.FoundText, ": 2 snippets,");
        }

        [TestMethod]
        public void TheStarterSnippetsShowWhenTheEngineWritesThem()
        {
            // QA-7: no snippet file yet, as on a first start before the engine has written one.
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            Assert.IsTrue(model.HasNoSnippets);
            Assert.AreEqual(0, environment.Watches.Count, "Nothing is watched until the page is shown.");

            model.Dispatch = action => action();
            FakeFileWatch watch = environment.Watches.Single();
            Assert.AreEqual(_local, watch.Folder);
            CollectionAssert.AreEqual(BothFileNames, watch.FileNames.ToArray(), "Either name counts.");

            watch.Trigger();
            Assert.IsTrue(model.HasNoSnippets, "Nothing has appeared yet.");

            WriteFile(LocalNewFile, "// header\n!ddate={{date}}\n!ttime={{time}}\n");
            watch.Trigger();

            Assert.IsFalse(model.HasNoSnippets);
            Assert.AreEqual(2, model.Snippets.Count);
            Assert.IsTrue(watch.Disposed, "With a file, nothing more is watched.");

            // Closing the page stops watching, even while there is no file.
            File.Delete(LocalNewFile);
            model.LoadSnippets();
            Assert.IsFalse(environment.Watches.Last().Disposed);
            model.Dispatch = null;
            Assert.IsTrue(environment.Watches.Last().Disposed);
            Assert.AreEqual(2, environment.Watches.Count);
        }

        [TestMethod]
        public void AConflictFileWithOnlyCarriageReturnsIsCountedTheWayTheEditorCountsIt()
        {
            WriteFile(LocalFile, LocalSnippets);
            WriteFile(WorkNewFile, "!a=1\r!b=2\r!c=3\r");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);

            StartSetup(model);

            Assert.IsTrue(model.Sync.HasConflict);
            Assert.AreEqual("OneDrive already has 3 snippets.", model.Sync.ConflictTitle, "FUZZ-5: CR-only line breaks were read as one line.");
        }

        [TestMethod]
        [DataRow("toggle off")]
        [DataRow("cancel")]
        [DataRow("first PC")]
        [DataRow("page closed")]
        public void TheSearchShowsWhileItRunsAndIsCancelledWhenTheSetupMovesOn(string how)
        {
            WriteFile(LocalFile, LocalSnippets);
            WriteFile(Path.Combine(_work, "Text Expander", "snippets.txt"), "!a=1\n");
            TextExpanderSyncTestEnvironment environment = CreateEnvironment(work: true);
            TextExpanderViewModel model = CreateViewModel(environment);
            var pending = new TaskCompletionSource();
            Action search = null;
            model.Sync.RunInBackground = work =>
            {
                search = work;
                return pending.Task;
            };
            StartSetup(model);

            model.Sync.SetupKindIndex = TextExpanderSyncViewModel.AnotherPcIndex;
            Assert.IsTrue(model.Sync.IsSearching, "The spinner shows while it looks.");
            Assert.IsFalse(model.Sync.HasCandidates);
            Assert.IsFalse(model.Sync.IsWaiting);

            switch (how)
            {
                case "toggle off":
                    Assert.AreEqual(TextExpanderSyncToggleAction.None, model.Sync.UserToggled(false));
                    break;
                case "cancel":
                    model.Sync.CancelSetup();
                    break;
                case "first PC":
                    model.Sync.SetupKindIndex = TextExpanderSyncViewModel.FirstPcIndex;
                    break;
                default:
                    model.Sync.LeaveSetup(pageClosing: true);
                    break;
            }

            Assert.IsFalse(model.Sync.IsSearching, how);

            // The search gets to run only now: it was cancelled first, so it lists nothing and shows nothing.
            search();
            pending.SetResult();

            Assert.AreEqual(0, environment.Listed.Count, how);
            Assert.IsFalse(model.Sync.IsSearching, how);
            Assert.IsFalse(model.Sync.HasCandidates, how);
            Assert.IsFalse(model.Sync.IsFound, how);
            Assert.IsFalse(model.Sync.IsWaiting, how);
            Assert.AreEqual(0, environment.Watches.Count, how);
        }

        private static void StartSetup(TextExpanderViewModel model)
        {
            Assert.AreEqual(TextExpanderSyncToggleAction.ShowSetup, model.Sync.UserToggled(true));
            Assert.IsTrue(model.Sync.IsSyncOn);
            Assert.IsTrue(model.Sync.IsSetupVisible);
        }

        private static string Resource(string key) => key switch
        {
            "TextExpander_Sync_AccountPersonal" => "OneDrive \u2013 Personal",
            "TextExpander_Sync_AccountWork" => "OneDrive \u2013 {0} (work or school)",
            "TextExpander_Sync_AccountGeneric" => "OneDrive",
            "TextExpander_Sync_FirstPcDescription" => "Copy my {0} snippets to OneDrive.",
            "TextExpander_Sync_FirstPcDescription_One" => "Copy my {0} snippet to OneDrive.",
            "TextExpander_Sync_ConflictTitle" => "OneDrive already has {0} snippets.",
            "TextExpander_Sync_ConflictTitle_One" => "OneDrive already has {0} snippet.",
            "TextExpander_Sync_ConflictUse" => "Use the {0} snippets in OneDrive",
            "TextExpander_Sync_ConflictUse_One" => "Use the {0} snippet in OneDrive",
            "TextExpander_Sync_ConflictReplace" => "Replace with this PC's {0} snippets",
            "TextExpander_Sync_ConflictReplace_One" => "Replace with this PC's {0} snippet",
            "TextExpander_Sync_StopDialog_Content" => "Copies OneDrive's snippets here.",
            "TextExpander_Sync_StopDialog_ContentBackup" => "Copies OneDrive's snippets here and keeps this PC's as {0}.",
            "TextExpander_Sync_ConflictKeepBothDescription" => "Use OneDrive's file, and save this PC's as {0}.",
            "TextExpander_Sync_Found" => "Found your snippets in {0}: {1} snippets, changed {2} at {3}.",
            "TextExpander_Sync_Found_One" => "Found your snippets in {0}: {1} snippet, changed {2} at {3}.",
            "TextExpander_Sync_Synced" => "Syncing with {0} \u00B7 {1} snippets",
            "TextExpander_Sync_Synced_One" => "Syncing with {0} \u00B7 {1} snippet",
            "TextExpander_Sync_SyncedCountUnavailable" => "Syncing with {0}",
            "TextExpander_SnippetFile_SummaryDefault" => "Saved on this PC \u00B7 {0} snippets",
            "TextExpander_SnippetFile_SummaryDefault_One" => "Saved on this PC \u00B7 {0} snippet",
            "TextExpander_SnippetFile_SummaryDefaultCountUnavailable" => "Saved on this PC",
            "TextExpander_SnippetFile_SummaryCustom" => "Saved in a folder you chose \u00B7 {0} snippets",
            "TextExpander_SnippetFile_SummaryCustom_One" => "Saved in a folder you chose \u00B7 {0} snippet",
            "TextExpander_SnippetFile_SummaryCustomCountUnavailable" => "Saved in a folder you chose",
            "TextExpander_SnippetFile_SummaryElevated" => "Not available while PowerToys runs as administrator",
            "TextExpander_Sync_AnnounceCopied" => "Your snippets are now syncing with {0}.",
            "TextExpander_Sync_AnnounceUsing" => "Text Expander now uses the snippets in {0}.",
            "TextExpander_Sync_AnnounceArrived" => "Found your snippets in {0}. Text Expander now uses them.",
            "TextExpander_Sync_AnnounceStopped" => "Stopped syncing. This PC now uses its own copy of your snippets.",
            "TextExpander_Sync_StopMissingFile" => "OneDrive's snippet file is no longer available. Text Expander is still using OneDrive.",
            "TextExpander_Sync_AnnounceKeepCopy" => "A copy of your snippets will be kept on this PC.",
            "TextExpander_Sync_AnnounceWaitingCancelled" => "Stopped looking for your snippets.",
            "TextExpander_SnippetCount" => "{0} of {1}",
            "TextExpander_SnippetsPath_Description" => "Your snippets are saved as {0} in this folder.",
            "TextExpander_Sync_CandidateDetails" => "{0} snippets \u00B7 {1}",
            "TextExpander_Sync_CandidateDetails_One" => "{0} snippet \u00B7 {1}",
            "TextExpander_Sync_AnnounceSearchFound" => "Found {0} snippet files in OneDrive.",
            "TextExpander_Sync_AnnounceSearchFound_One" => "Found {0} snippet file in OneDrive.",
            "TextExpander_Sync_FoundLocation" => "Location: {0}",
            "TextExpander_Sync_WaitingDefaultLocation" => "Default location: {0}",
            "TextExpander_Sync_WaitingLocation" => "Location: {0}",
            "TextExpander_Sync_NotFoundMessage" => "Text Expander checked the default location and searched your OneDrive. If OneDrive is still downloading them, Text Expander will start using them when they arrive.",
            "TextExpander_Sync_NotFoundChosenMessage" => "Text Expander checked the folder you chose. If OneDrive is still downloading them, Text Expander will start using them when they arrive.",
            "TextExpander_Sync_AnnounceNotFound" => "Couldn't find your snippets in OneDrive.",
            _ => key,
        };

        private static SettingsUtils CreateSettingsUtils(TextExpanderSettings settings, System.Collections.Generic.List<string> saved = null)
        {
            // Not CallBase, so saving settings never writes the real settings file.
            var utils = new Mock<SettingsUtils>(new FileSystem(), null);
            utils.Setup(u => u.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
            if (saved != null)
            {
                utils.Setup(u => u.SaveSettings(It.IsAny<string>(), TextExpanderSettings.ModuleName, It.IsAny<string>()))
                    .Callback<string, string, string>((json, _, _) => saved.Add(json));
            }

            return utils.Object;
        }

        private static ISettingsRepository<GeneralSettings> CreateRepository()
        {
            var repository = new Mock<ISettingsRepository<GeneralSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(new GeneralSettings());
            return repository.Object;
        }

        private static byte[] WriteFile(string path, string text, bool bom = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var encoding = new UTF8Encoding(bom);
            byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            File.WriteAllBytes(path, bytes);
            return bytes;
        }

        private TextExpanderSyncTestEnvironment CreateEnvironment(bool personal = false, bool work = false, string generic = null)
        {
            var environment = new TextExpanderSyncTestEnvironment(_root, _local);
            if (personal)
            {
                environment.Variables["OneDriveConsumer"] = _personal;
            }

            if (work)
            {
                environment.Variables["OneDriveCommercial"] = _work;
            }

            if (generic != null)
            {
                environment.Variables["OneDrive"] = generic;
            }

            return environment;
        }

        private TextExpanderViewModel CreateViewModel(TextExpanderSyncTestEnvironment environment, string snippetsPath = "", Func<string, Task<string>> pickFolder = null)
        {
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = snippetsPath;
            return SearchInPlace(new TextExpanderViewModel(CreateSettingsUtils(settings), CreateRepository(), _ => 0, pickFolder, isElevated: false, Resource, environment));
        }

        private TextExpanderViewModel CreateViewModel(TextExpanderSyncTestEnvironment environment, TextExpanderSettings settings, System.Collections.Generic.List<string> saved = null)
            => SearchInPlace(new TextExpanderViewModel(CreateSettingsUtils(settings, saved), CreateRepository(), _ => 0, null, isElevated: false, Resource, environment));

        /// <summary>Runs the OneDrive search in place, so a test sees its result right away.</summary>
        private static TextExpanderViewModel SearchInPlace(TextExpanderViewModel model)
        {
            model.Sync.RunInBackground = work =>
            {
                work();
                return Task.CompletedTask;
            };
            return model;
        }
    }
}

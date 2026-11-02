// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Choice = Microsoft.PowerToys.TextExpander.Editor.ViewModels.TextExpanderFileConflictChoice;

namespace ViewModelTests
{
    /// <summary>
    /// The snippets file the user picks: where the editor says it is (this PC, OneDrive or its folder) and how it shows its
    /// path, moving the snippets to a file or using one, the sync advice, and a chosen file that's missing or can't be
    /// reached, which is reported and never created.
    /// </summary>
    [TestClass]
    public class TextExpanderFileFlow
    {
        private const string Current = TextExpanderSnippetFileNames.Current;
        private static readonly string TwoSnippets = SnippetJson.Of("!sig=Regards", "!email=alex@contoso.com");

        private static readonly string[] WorkFile = { "work.json" };

        private static readonly Lazy<Dictionary<string, string>> English = new(() => RepositoryFiles.Xml(RepositoryFiles.EditorResources)
            .Descendants("data")
            .ToDictionary(data => (string)data.Attribute("name"), data => data.Element("value").Value));

        private string _root;
        private FlowEnvironment _environment;

        private string OneDrive => Path.Combine(_root, "OneDrive - Contoso");

        private string Synced => Path.Combine(_root, "Synced", "Snippets");

        /// <summary>Gets this PC's own folder, the default.</summary>
        private string Local => _environment.DefaultSnippetsFolder;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-Files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _environment = new FlowEnvironment(Path.Combine(_root, "local"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_root, recursive: true);
        }

        [TestMethod]
        [DataRow("OneDrive")]
        [DataRow("OneDriveConsumer")]
        [DataRow("OneDriveCommercial")]
        public void AFileInOneDriveIsSavedInOneDrive(string variable)
        {
            string file = Write(Path.Combine(OneDrive, "Documents", "Text Expander", Current), TwoSnippets);
            Assert.AreEqual("Saved in Text Expander", CreateViewModel(file).SnippetFolderLabel, "Only the variables say where OneDrive is.");

            _environment.Variables[variable] = OneDrive + Path.DirectorySeparatorChar;
            TextExpanderViewModel model = CreateViewModel(file);

            Assert.AreEqual("Saved in OneDrive", model.SnippetFolderLabel);
            Assert.AreEqual("Saved in OneDrive · 2 snippets", model.SnippetFileSummary);
            Assert.IsTrue(model.IsSnippetsFileInOneDrive);
            Assert.AreEqual(file, model.SnippetsFilePath, "The tooltip and copying get the full path.");
            Assert.IsTrue(model.SnippetsFileDisplayPath.Length <= 69, model.SnippetsFileDisplayPath);
            StringAssert.EndsWith(model.SnippetsFileDisplayPath, "\\\u2026\\" + Current);
            Assert.AreEqual(Path.GetDirectoryName(file), model.SnippetsPickerFolder);
        }

        [TestMethod]
        public void OtherFilesAreSavedInTheirFolderAndTheDefaultOnThisPc()
        {
            _environment.Variables["OneDrive"] = OneDrive;
            string work = Write(Path.Combine(Synced, "work.json"), SnippetJson.Of("!one=1"));
            string beside = Write(Path.Combine(OneDrive + " (old)", "snippets.json"), TwoSnippets);

            TextExpanderViewModel model = CreateViewModel(work);
            Assert.AreEqual("Saved in Snippets", model.SnippetFolderLabel);
            Assert.AreEqual("Saved in Snippets · 1 snippet", model.SnippetFileSummary);
            Assert.IsFalse(model.IsSnippetsFileInOneDrive);
            Assert.AreEqual("Saved in OneDrive - Contoso (old)", CreateViewModel(beside).SnippetFolderLabel, "A folder whose name only starts like OneDrive's isn't in it.");

            TextExpanderViewModel local = CreateViewModel(null);
            Assert.AreEqual("Saved on this PC", local.SnippetFolderLabel);
            Assert.AreEqual("Saved on this PC · 6 snippets", local.SnippetFileSummary);
            Assert.IsNull(local.SnippetsPickerFolder, "The pickers open in Documents rather than this PC's hidden folder.");
        }

        [TestMethod]
        public void TheSyncAdviceIsTwoStepsUntilTheFileIsInOneDrive()
        {
            _environment.Variables["OneDrive"] = OneDrive;
            TextExpanderViewModel local = CreateViewModel(null);

            Assert.IsFalse(local.IsSnippetsFileInOneDrive);
            Assert.AreEqual("Use your snippets on all your PCs", local.SyncHeading);
            CollectionAssert.AreEqual(
                new[]
                {
                    new TextExpanderSyncStep("Select ", "Move snippets database\u2026", " and save it in OneDrive or another synced folder."),
                    new TextExpanderSyncStep("On each of your other PCs, select ", "Sync from another PC\u2026", " and pick that database."),
                },
                local.SyncSteps.ToArray());

            TextExpanderViewModel inOneDrive = CreateViewModel(Write(Path.Combine(OneDrive, "Text Expander", Current), TwoSnippets));

            Assert.IsTrue(inOneDrive.IsSnippetsFileInOneDrive);
            Assert.AreEqual("Use these snippets on your other PCs", inOneDrive.SyncHeading);
            TextExpanderSyncStep note = inOneDrive.SyncSteps.Single();
            Assert.AreEqual("Sync from another PC\u2026", note.Button, "The button's name is shown bold.");
            Assert.AreEqual("On each PC, select Sync from another PC\u2026 and pick this database.", note.Before + note.Button + note.After);
        }

        [TestMethod]
        public void OneDriveIsRecognizedOnlyByTheFoldersItsVariablesName()
        {
            const string Root = @"C:\Users\alex\OneDrive - Contoso";
            Func<string, string> variables = value => value == "%OneDriveCommercial%" ? Root : value;

            Assert.IsTrue(TextExpanderFolderText.IsInOneDrive(Root, variables));
            Assert.IsTrue(TextExpanderFolderText.IsInOneDrive(Root + @"\Documents\Text Expander\", variables));
            Assert.IsFalse(TextExpanderFolderText.IsInOneDrive(Root + @"2\Text Expander", variables));
            Assert.IsFalse(TextExpanderFolderText.IsInOneDrive(@"C:\Users\alex\Documents", variables));
            Assert.IsFalse(TextExpanderFolderText.IsInOneDrive(Root, value => value), "Unset variables come back unexpanded.");
            Assert.IsFalse(TextExpanderFolderText.IsInOneDrive(Root, _ => "OneDrive"), "A relative value isn't a folder.");
            Assert.IsFalse(TextExpanderFolderText.IsInOneDrive(string.Empty, variables));

            Assert.AreEqual("Text Expander", TextExpanderFolderText.FolderName(Root + @"\Text Expander\"));
            Assert.AreEqual(@"D:\", TextExpanderFolderText.FolderName(@"D:\"));
        }

        [TestMethod]
        public void LongPathsKeepTheirStartAndFileNameOnOneLine()
        {
            const string InOneDrive = @"C:\Users\alex\OneDrive - Contoso\Documents\Text Expander\PowerToysTextExpanderSnippets.json";
            const string Default = @"C:\Users\alex\AppData\Local\Microsoft\PowerToys\TextExpander\PowerToysTextExpanderSnippets.json";

            Assert.AreEqual(@"C:\Users\alex\OneDrive - Contoso\…\PowerToysTextExpanderSnippets.json", TextExpanderFolderText.TrimMiddle(InOneDrive, 69));
            Assert.AreEqual(@"C:\Users\alex\AppData\Local\…\PowerToysTextExpanderSnippets.json", TextExpanderFolderText.TrimMiddle(Default, 69));
            Assert.AreEqual(@"C:\…\PowerToysTextExpanderSnippets.json", TextExpanderFolderText.TrimMiddle(InOneDrive, 20), "The root and the name always stay.");
            Assert.AreEqual(InOneDrive, TextExpanderFolderText.TrimMiddle(InOneDrive, InOneDrive.Length), "A path that fits stays whole.");
            Assert.AreEqual(@"\\server\share\team\…\work.json", TextExpanderFolderText.TrimMiddle(@"\\server\share\team\snippets\work.json", 31));
            Assert.AreEqual(@"D:\A long file name.json", TextExpanderFolderText.TrimMiddle(@"D:\A long file name.json", 10), "There's no folder between the root and the name to drop.");
            Assert.AreEqual(string.Empty, TextExpanderFolderText.TrimMiddle(null, 68));
        }

        [TestMethod]
        public void OnlyErrorsThatMeanTheFileCantBeReachedAreMapped()
        {
            // Path, drive, device, network and cloud-provider errors.
            foreach (int code in new[] { 3, 15, 21, 53, 55, 64, 67, 362, 404, 1231 })
            {
                Assert.IsTrue(TextExpanderFolderText.IsUnreachable(Win32Error(code)), code.ToString(CultureInfo.InvariantCulture));
            }

            Assert.IsTrue(TextExpanderFolderText.IsUnreachable(new DirectoryNotFoundException()));
            Assert.IsFalse(TextExpanderFolderText.IsUnreachable(new FileNotFoundException()), "A missing file is told apart from its folder.");
            Assert.IsFalse(TextExpanderFolderText.IsUnreachable(Win32Error(32)), "Another app has the file open.");
            Assert.IsFalse(TextExpanderFolderText.IsUnreachable(new UnauthorizedAccessException()));
            Assert.IsFalse(TextExpanderFolderText.IsUnreachable(new IOException("No Windows error.")));
        }

        [TestMethod]
        public void AChosenFileThatIsGoneIsReportedAndNeverCreated()
        {
            _environment.Variables["OneDriveCommercial"] = OneDrive;
            string folder = Path.Combine(OneDrive, "Text Expander");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, Current);

            TextExpanderViewModel model = CreateViewModel(file);

            Assert.AreEqual(0, Directory.GetFileSystemEntries(folder).Length, "Neither the starter nor anything else is written there.");
            Assert.IsTrue(model.HasSnippetError);
            Assert.IsTrue(model.IsSnippetsFileUnavailable);
            Assert.IsTrue(model.IsSnippetsFileMissing);
            Assert.IsFalse(model.CanAddSnippets);
            Assert.AreEqual("Can't find your snippets database", model.SnippetErrorTitle);
            Assert.AreEqual(Resource("TextExpander_SnippetError_CantFindHelp"), model.SnippetError);
            Assert.AreEqual(file, model.SnippetsFilePath);
            Assert.AreEqual("Saved in OneDrive", model.SnippetFolderLabel);

            // Retry finds it once it's back.
            Write(file, TwoSnippets);
            model.LoadSnippets();

            Assert.IsFalse(model.HasSnippetError);
            Assert.IsFalse(model.IsSnippetsFileUnavailable);
            Assert.AreEqual(2, model.Snippets.Count);
        }

        [TestMethod]
        public void AChosenFileWhoseFolderIsGoneCantBeReached()
        {
            _environment.Variables["OneDrive"] = OneDrive;
            TextExpanderViewModel model = CreateViewModel(Path.Combine(OneDrive, "Gone", "work.json"));

            Assert.IsFalse(Directory.Exists(Path.Combine(OneDrive, "Gone")), "No folder is created, and none named after the file.");
            Assert.IsTrue(model.IsSnippetsFileUnavailable);
            Assert.IsFalse(model.IsSnippetsFileMissing);
            Assert.AreEqual("Can't reach your snippets in OneDrive", model.SnippetErrorTitle);
            Assert.AreEqual(Resource("TextExpander_SnippetError_CantReachOneDriveHelp"), model.SnippetError);

            TextExpanderViewModel elsewhere = CreateViewModel(Path.Combine(_root, "USB", "Snippets", "work.json"));
            Assert.AreEqual("Can't reach your snippets in Snippets", elsewhere.SnippetErrorTitle);
            Assert.AreEqual(Resource("TextExpander_SnippetError_CantReachCustomHelp"), elsewhere.SnippetError);

            // A path is the file, whatever its name, so one without a .json name isn't taken for a folder to create.
            string folder = Path.Combine(_root, "USB", "Folder");
            TextExpanderViewModel folderModel = CreateViewModel(folder);
            Assert.AreEqual("Can't reach your snippets in USB", folderModel.SnippetErrorTitle);
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "USB")));
            Assert.IsFalse(Directory.Exists(Local), "The editor doesn't switch to this PC's folder instead.");
        }

        [TestMethod]
        public void AFolderInTheSettingIsAFileThatIsNotThere()
        {
            // Earlier builds saved a folder here. It's never read as one, and nothing is created in it.
            Directory.CreateDirectory(Synced);
            TextExpanderViewModel model = CreateViewModel(Synced);

            Assert.IsTrue(model.IsSnippetsFileMissing);
            Assert.AreEqual("Can't find your snippets database", model.SnippetErrorTitle);
            Assert.AreEqual(Resource("TextExpander_SnippetError_CantFindHelp"), model.SnippetError);
            Assert.AreEqual(Synced, model.SnippetsFilePath);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(Synced).Length);
        }

        [TestMethod]
        public void TheDefaultFolderIsStillCreatedWithTheStarter()
        {
            TextExpanderViewModel model = CreateViewModel(null);

            Assert.IsFalse(model.HasSnippetError);
            Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(Path.Combine(Local, Current)));
            Assert.AreEqual(6, model.Snippets.Count);

            // Saving brings back only this PC's own folder.
            Directory.Delete(Local, recursive: true);
            Assert.IsNull(model.SaveSnippet(null, "!new", "New"));
            CollectionAssert.Contains(SnippetJson.In(Path.Combine(Local, Current)), "!new=New");
        }

        [TestMethod]
        public void SavingToAChosenFileThatIsGoneDoesNotWriteItAgain()
        {
            string file = Write(Path.Combine(Synced, "work.json"), SnippetJson.Of("!one=1"));
            TextExpanderViewModel model = CreateViewModel(file);
            File.Delete(file);

            Assert.AreEqual(Resource("TextExpander_SnippetError_CantFindHelp"), model.SaveSnippet(null, "!two", "2"));

            Assert.IsTrue(model.IsSnippetsFileMissing);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(Synced).Length, "It may have been moved or renamed on another PC.");
        }

        [TestMethod]
        public void OtherErrorsKeepTheirMessageWithoutOfferingAnotherFile()
        {
            string file = Write(Path.Combine(Synced, "work.json"), SnippetJson.Of("!one=1"));
            TextExpanderViewModel model = CreateViewModel(file);

            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                model.LoadSnippets();
            }

            Assert.IsTrue(model.HasSnippetError);
            Assert.IsFalse(model.IsSnippetsFileUnavailable);
            Assert.AreEqual("Can't open your snippets", model.SnippetErrorTitle);
        }

        [TestMethod]
        public void MovingWritesTheSnippetsToTheNewFileAndUsesIt()
        {
            _environment.Variables["OneDrive"] = OneDrive;
            string local = Write(Path.Combine(Local, Current), TwoSnippets);
            byte[] bytes = File.ReadAllBytes(local);
            string folder = Path.Combine(OneDrive, "Text Expander");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, Current);
            TextExpanderViewModel model = CreateViewModel(null);

            Assert.IsFalse(model.FileHasSnippets(target));
            Assert.IsNull(model.MoveSnippetsTo(target, Choice.ReplaceFile, out string status));

            Assert.AreEqual("Your 2 snippets are now saved in OneDrive.", status);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(target), "The file is copied as it is.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(local), "The previous file stays as it was.");
            CollectionAssert.AreEqual(new[] { target }, Directory.GetFiles(folder), "No temporary file is left.");
            Assert.AreEqual(target, model.SnippetsPath, "The setting names the file.");
            Assert.AreEqual(target, model.SnippetsFilePath);
            Assert.AreEqual("Saved in OneDrive · 2 snippets", model.SnippetFileSummary);
        }

        [TestMethod]
        public void MovingTheSnippetsOntoTheirOwnFileDoesNothing()
        {
            string file = Write(Path.Combine(Synced, "work.json"), TwoSnippets);
            TextExpanderViewModel model = CreateViewModel(file);

            foreach (string same in new[] { file, file.ToUpperInvariant() })
            {
                Assert.IsFalse(model.FileHasSnippets(same), "There's nothing to ask about.");
                Assert.IsNull(model.MoveSnippetsTo(same, Choice.ReplaceFile, out string status));
                Assert.IsNull(status, "Nor anything to say.");
            }

            Assert.AreEqual(TwoSnippets, File.ReadAllText(file));
            CollectionAssert.AreEqual(new[] { file }, Directory.GetFiles(Synced), "Nothing is written or backed up.");
            Assert.AreEqual(file, model.SnippetsPath);
        }

        [TestMethod]
        public void MovingOntoAFileBacksItUpFirst()
        {
            Write(Path.Combine(Local, Current), TwoSnippets);
            string none = SnippetJson.Of();
            string empty = Write(Path.Combine(Synced, "work.json"), none);
            string shared = Write(Path.Combine(Synced, "shared.json"), SnippetJson.Of("!old=Old"));
            TextExpanderViewModel model = CreateViewModel(null);

            // A file without snippets is replaced without the question, after it's backed up.
            Assert.IsFalse(model.FileHasSnippets(empty));
            Assert.IsNull(model.MoveSnippetsTo(empty, Choice.ReplaceFile, out string status));
            Assert.AreEqual("Your 2 snippets are now saved in Snippets. The database you replaced was backed up.", status);
            Assert.AreEqual(none, File.ReadAllText(Directory.GetFiles(Synced, "work.json.backup-*").Single()));

            // One with snippets gets the question; Replace them backs it up, then writes these snippets.
            Assert.IsTrue(model.FileHasSnippets(shared));
            Assert.IsNull(model.MoveSnippetsTo(shared, Choice.ReplaceFile, out _));
            Assert.AreEqual(SnippetJson.Of("!old=Old"), File.ReadAllText(Directory.GetFiles(Synced, "shared.json.backup-*").Single()));
            Assert.AreEqual(TwoSnippets, File.ReadAllText(shared));
            Assert.AreEqual(shared, model.SnippetsPath);
        }

        [TestMethod]
        public void UseItsSnippetsSwitchesToTheFileAndWritesNothing()
        {
            _environment.Variables["OneDrive"] = OneDrive;
            string local = Write(Path.Combine(Local, Current), SnippetJson.Of("!mine=Mine"));
            string shared = Write(Path.Combine(OneDrive, "Text Expander", Current), TwoSnippets);
            TextExpanderViewModel model = CreateViewModel(null);

            Assert.IsTrue(model.FileHasSnippets(shared));
            Assert.IsNull(model.MoveSnippetsTo(shared, Choice.UseFile, out string status));

            Assert.AreEqual("Now using the 2 snippets in OneDrive.", status);
            Assert.AreEqual(TwoSnippets, File.ReadAllText(shared));
            Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(shared)).Length, "Nothing is written or backed up.");
            Assert.AreEqual(SnippetJson.Of("!mine=Mine"), File.ReadAllText(local));
            Assert.AreEqual(shared, model.SnippetsPath);
        }

        [TestMethod]
        public void MovingRefusesWhenTheFileChangedSinceItWasRead()
        {
            string local = Write(Path.Combine(Local, Current), TwoSnippets);
            TextExpanderViewModel model = CreateViewModel(null);
            File.WriteAllText(local, SnippetJson.Of("!changed=Elsewhere"));
            Directory.CreateDirectory(Synced);
            string target = Path.Combine(Synced, "work.json");

            Assert.AreEqual(Resource("TextExpander_FileChangedBeforeSave"), model.MoveSnippetsTo(target, Choice.ReplaceFile, out string status));

            Assert.IsNull(status);
            Assert.IsFalse(File.Exists(target));
            Assert.AreEqual("!changed", model.Snippets.Single().Trigger, "The list shows the file as it is now.");
            Assert.AreEqual(string.Empty, model.SnippetsPath);
        }

        [TestMethod]
        public void UsingAFileSwitchesWithoutWritingAnything()
        {
            _environment.Variables["OneDrive"] = OneDrive;
            Write(Path.Combine(Local, Current), SnippetJson.Of("!mine=Mine"));
            string single = Write(Path.Combine(OneDrive, "Text Expander", Current), SnippetJson.Of("!sig=Regards"));
            string work = Write(Path.Combine(Synced, "work.json"), TwoSnippets);
            TextExpanderViewModel model = CreateViewModel(null);

            Assert.IsNull(model.UseSnippetsFile(single, out string status));
            Assert.AreEqual("Now using the 1 snippet in OneDrive.", status);
            Assert.AreEqual(single, model.SnippetsPath);
            Assert.AreEqual(SnippetJson.Of("!sig=Regards"), File.ReadAllText(single));

            Assert.IsNull(model.UseSnippetsFile(work, out status));
            Assert.AreEqual("Now using the 2 snippets in Snippets.", status);
            Assert.AreEqual(1, Directory.GetFiles(Synced).Length);
        }

        [TestMethod]
        [DataRow("notes.json", "Buy milk\nCall Alex\n", DisplayName = "notes")]
        [DataRow("empty.json", "", DisplayName = "an empty file")]
        [DataRow("comments.json", "// Snippets for work\n", DisplayName = "only a comment")]
        [DataRow("settings.json", "{ \"theme\": \"dark\" }", DisplayName = "JSON without a list of snippets")]
        [DataRow("old.txt", "!sig=Regards\n", DisplayName = "the old text format")]
        public void AFileThatIsntASnippetsFileIsRefused(string name, string content)
        {
            Write(Path.Combine(Local, Current), SnippetJson.Of("!mine=Mine"));
            string file = Write(Path.Combine(Synced, name), content);
            TextExpanderViewModel model = CreateViewModel(null);

            Assert.AreEqual(name + " doesn't look like a snippets database", model.UseSnippetsFile(file, out string status));
            Assert.IsNull(status);
            Assert.AreEqual(string.Empty, model.SnippetsPath, "Text Expander doesn't switch.");
            Assert.AreEqual(content, File.ReadAllText(file));
        }

        [TestMethod]
        public void AnEmptyListIsASnippetsFile()
        {
            Write(Path.Combine(Local, Current), SnippetJson.Of("!mine=Mine"));
            string none = Write(Path.Combine(Synced, "none.json"), SnippetJson.Of());
            TextExpanderViewModel model = CreateViewModel(null);

            Assert.IsNull(model.UseSnippetsFile(none, out string status));
            Assert.AreEqual("Now using the 0 snippets in Snippets.", status);
            Assert.AreEqual(none, model.SnippetsPath);
            Assert.IsTrue(model.HasNoSnippets);
        }

        [TestMethod]
        public void UsingAnotherFileRecoversFromAMissingOne()
        {
            Directory.CreateDirectory(Synced);
            string gone = Path.Combine(Synced, "gone.json");
            string work = Write(Path.Combine(Synced, "work.json"), TwoSnippets);
            TextExpanderViewModel model = CreateViewModel(gone);
            Assert.IsTrue(model.IsSnippetsFileMissing);

            Assert.IsNull(model.UseSnippetsFile(work, out _));

            Assert.IsFalse(model.HasSnippetError);
            Assert.IsFalse(File.Exists(gone));
            Assert.AreEqual(2, model.Snippets.Count);
        }

        [TestMethod]
        public void ASettingsFailureKeepsTheFileInUse()
        {
            Write(Path.Combine(Local, Current), SnippetJson.Of("!mine=Mine"));
            string work = Write(Path.Combine(Synced, "work.json"), TwoSnippets);
            Mock<TextExpanderSettingsUtils> store = EditorSettings.Store(EditorSettings.For(string.Empty));
            store.Setup(s => s.UpdateSnippetsPath(It.IsAny<string>()))
                .Throws(new AtomicSettingsPublicationException(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, "settings.json", recoveryPaths: null, new IOException("Injected settings write failure.")));
            TextExpanderViewModel model = new(store.Object, isElevated: false, Resource, _environment);

            Assert.AreEqual(Resource("TextExpander_SnippetFolder_SettingsSaveFailed"), model.UseSnippetsFile(work, out string status));

            Assert.IsNull(status);
            Assert.AreEqual(string.Empty, model.SnippetsPath);
            Assert.AreEqual("!mine", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void AChosenFileIsWatchedByItsOwnName()
        {
            string file = Write(Path.Combine(Synced, "work.json"), SnippetJson.Of("!a=A"));
            using TextExpanderViewModel model = CreateViewModel(file);
            model.Dispatch = action => action();

            Assert.AreEqual(Synced, _environment.WatchedFolder);
            CollectionAssert.AreEqual(WorkFile, _environment.WatchedNames.ToArray());

            File.WriteAllText(file, SnippetJson.Of("!a=A", "!b=B"));
            _environment.NotifyFilesChanged();
            Assert.AreEqual(2, model.Snippets.Count, "A change that syncs in updates the list.");

            File.Delete(file);
            _environment.NotifyFilesChanged();
            Assert.IsTrue(model.IsSnippetsFileMissing);
            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void EveryStringTheFileTextIsBuiltFromExists()
        {
            foreach (string place in new[] { "Default", "OneDrive", "Custom" })
            {
                foreach (string prefix in new[] { "TextExpander_SnippetFile_Summary", "TextExpander_SnippetFile_Saved", "TextExpander_SnippetFile_Using" })
                {
                    Assert.IsTrue(English.Value.ContainsKey(prefix + place), prefix + place);
                    Assert.IsTrue(English.Value.ContainsKey(prefix + place + "_One"), prefix + place + "_One");
                }

                Assert.IsTrue(English.Value.ContainsKey("TextExpander_SnippetFile_Summary" + place + "CountUnavailable"), place);
            }

            foreach (string key in new[] { "TextExpander_SnippetError_CantFind", "TextExpander_SnippetError_CantReachOneDrive", "TextExpander_SnippetError_CantReachCustom" })
            {
                Assert.IsTrue(English.Value.ContainsKey(key), key);
                Assert.IsTrue(English.Value.ContainsKey(key + "Help"), key + "Help");
            }
        }

        private static IOException Win32Error(int code) => new(new Win32Exception(code).Message, unchecked((int)0x80070000) | code);

        private static string Write(string file, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, content);
            return file;
        }

        /// <summary>
        /// Gets the English text of <paramref name="key"/>, which names a property as the page's x:Uid does, or the key itself
        /// when the resources don't have it.
        /// </summary>
        private static string Resource(string key) => English.Value.TryGetValue(key.Replace('/', '.'), out string value) ? value : key;

        private TextExpanderViewModel CreateViewModel(string snippetsPath)
            => new(EditorSettings.Store(EditorSettings.For(snippetsPath ?? string.Empty)).Object, isElevated: false, Resource, _environment);

        /// <summary>
        /// This PC's file operations, with a scratch default folder, only the environment variables a test sets, and a
        /// watcher the test triggers.
        /// </summary>
        private sealed class FlowEnvironment : ITextExpanderStorageEnvironment
        {
            private readonly TextExpanderStorageEnvironment _files = new();
            private Action _changed;

            public FlowEnvironment(string defaultSnippetsFolder)
            {
                DefaultSnippetsFolder = defaultSnippetsFolder;
            }

            public string DefaultSnippetsFolder { get; }

            public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

            public string WatchedFolder { get; private set; }

            public IReadOnlyList<string> WatchedNames { get; private set; }

            public string ExpandEnvironmentVariables(string value)
            {
                foreach (KeyValuePair<string, string> variable in Variables)
                {
                    value = value.Replace("%" + variable.Key + "%", variable.Value, StringComparison.OrdinalIgnoreCase);
                }

                return value;
            }

            public bool FileExists(string path) => _files.FileExists(path);

            public void CreateDirectory(string path) => _files.CreateDirectory(path);

            public void WriteNewFile(string path, byte[] bytes) => _files.WriteNewFile(path, bytes);

            public void MoveFile(string source, string destination) => _files.MoveFile(source, destination);

            public void ReplaceFile(string source, string destination, string backup) => _files.ReplaceFile(source, destination, backup);

            public HandleBoundFileDeletionResult DeleteOwnedFile(string path, byte[] expectedBytes) => _files.DeleteOwnedFile(path, expectedBytes);

            public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            {
                WatchedFolder = folder;
                WatchedNames = fileNames;
                _changed = changed;
                return new CallbackDisposable(() => _changed = ReferenceEquals(_changed, changed) ? null : _changed);
            }

            public void NotifyFilesChanged() => _changed?.Invoke();
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

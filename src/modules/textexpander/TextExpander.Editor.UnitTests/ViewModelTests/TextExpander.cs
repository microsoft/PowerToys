// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class TextExpander
    {
        private static readonly string[] ExpectedTriggers = { "#sig", "#iso", "#email" };
        private static readonly string[] SigTrigger = { "!sig" };
        private static readonly string[] BtwTrigger = { "btw" };
        private static readonly string[] SymbolTrigger = { "!!" };
        private static readonly string[] AAndAb = { "a", "ab" };
        private static readonly string[] SigAndLongerCopy = { "!sig", "!2sigs" };
        private static readonly string[] SigAndHyphenatedCopy = { "!sig", "!2sig-long" };
        private static readonly string[] SampleAndCopy = { "!sample", "!2sample" };
        private static readonly string[] SampleAndTwoCopies = { "!sample", "!2sample", "!3sample" };
        private static readonly string[] LastCopy = { "!999sample" };
        private static readonly string[] Bond = { "!007bond" };
        private static readonly string[] VariableTokens = { "$|$", "{{date}}", "{{time}}", "{{datetime}}", "{{isodate}}", "{{date:dddd}}", "{{clipboard}}", "{{prompt:Label}}" };
        private static readonly string[] GermanVariableSamples = { "18.10.2026", "16:02", "18.10.2026 16:02", "2026-10-18", "Sonntag" };
        private string _directory;
        private string _path;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "snippets.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [TestMethod]
        public void SearchMatchesTriggersAndAllReplacementLinesWithoutReordering()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Regards,\nYour Name", "#iso={{isodate}}", "#email=you@example.com"));
            TextExpanderViewModel model = CreateViewModel();
            Assert.AreEqual(3, model.FilteredSnippets.Count);
            Assert.AreEqual("3 snippets", model.SnippetCountText);

            model.SearchText = "  NAME ";
            Assert.AreEqual("#sig", model.FilteredSnippets.Single().Trigger);
            Assert.AreEqual("1 of 3", model.SnippetCountText);
            Assert.IsTrue(model.HasSearchText);
            Assert.IsFalse(model.HasNoSnippets);

            model.SearchText = "#ISO";
            Assert.AreEqual("#iso", model.FilteredSnippets.Single().Trigger);

            model.SearchText = "missing";
            Assert.AreEqual(0, model.FilteredSnippets.Count);
            Assert.IsTrue(model.HasNoMatchingSnippets);
            Assert.IsFalse(model.HasNoSnippets);

            model.SearchText = string.Empty;
            CollectionAssert.AreEqual(ExpectedTriggers, model.FilteredSnippets.Select(s => s.Trigger).ToArray());
            Assert.IsFalse(model.HasSearchText);
            Assert.IsFalse(model.HasNoMatchingSnippets);
            Assert.AreEqual("3 snippets", model.SnippetCountText);
        }

        [TestMethod]
        public void SettingsDefaultToCaseSensitiveMatching()
        {
            var settings = new TextExpanderSettings();

            Assert.IsTrue(settings.Properties.CaseSensitive);
            StringAssert.Contains(settings.ToJsonString(), "\"case_sensitive\":true");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NewSnippetCreationKeepsTheBoundaryWaitingSetting(bool turnedOff)
        {
            var settings = new TextExpanderSettings();
            if (turnedOff)
            {
                settings.Properties.RequireWordBoundary = false;
            }

            TextExpanderViewModel model = CreateViewModel(settings: settings, snippets: SnippetJson.Of());

            Assert.AreEqual(!turnedOff, model.RequireWordBoundary, "Waiting is on by default.");
            Assert.IsNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual(!turnedOff, model.RequireWordBoundary);
        }

        [TestMethod]
        public void CaseSensitivityDefaultsOnAndShowsBothFormsWhenTurnedOff()
        {
            File.WriteAllText(_path, SnippetJson.Of("!Hello=Hello"));
            var latest = new TextExpanderSettings();
            latest.Properties.CaseSensitive = false;
            TextExpanderViewModel model = CreateViewModel(latest: latest);

            Assert.IsTrue(model.CaseSensitive);
            Assert.AreEqual("!Hello", model.Snippets.Single().TriggerDisplay);
            Assert.IsNull(model.ValidateSnippet(null, "!HELLO"), "Different capitalization is distinct by default.");

            // Settings turns it off.
            model.RefreshSettings();

            Assert.IsFalse(model.CaseSensitive);
            Assert.AreEqual("!hello / !HELLO", model.Snippets.Single().TriggerDisplay);
            Assert.AreEqual("Duplicate: !Hello", model.ValidateSnippet(null, "!HELLO"));
            StringAssert.Contains(model.Snippets.Single().AutomationName, "TextExpander_Row_CaseInsensitiveAutomation");
        }

        [TestMethod]
        public void ReloadAndSaveRetainCurrentSearchAndRefreshResults()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Your name"));
            TextExpanderViewModel model = CreateViewModel();
            model.SearchText = "date";
            Assert.IsTrue(model.HasNoMatchingSnippets);

            Assert.IsNull(model.SaveSnippet(null, "#iso", "{{isodate}}"));
            Assert.AreEqual("#iso", model.FilteredSnippets.Single().Trigger);
            Assert.AreEqual("date", model.SearchText);

            File.WriteAllText(_path, SnippetJson.Of("#changed=New date"));
            model.LoadSnippets();
            Assert.AreEqual("#changed", model.FilteredSnippets.Single().Trigger);
        }

        [TestMethod]
        public void AMissingChosenFileIsReportedAndNeverCreated()
        {
            TextExpanderViewModel model = CreateViewModel();

            Assert.AreEqual(_path, model.SnippetsFilePath);
            Assert.IsTrue(model.HasSnippetError);
            Assert.IsTrue(model.IsSnippetsFileMissing);
            Assert.AreEqual("TextExpander_SnippetError_CantFind", model.SnippetErrorTitle);
            Assert.AreEqual(0, model.Snippets.Count);
            Assert.IsFalse(model.HasNoSnippets, "Not an empty list, which would invite recreating every snippet.");
            Assert.IsNotNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual(0, Directory.GetFiles(_directory).Length, "Nothing is created where the file was.");
        }

        [TestMethod]
        public void ReadFailureIsNotAnEmptyState()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Example"));
            TextExpanderViewModel model = CreateViewModel();
            using var lockedFile = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            model.LoadSnippets();

            Assert.IsTrue(model.HasSnippetError);
            Assert.AreEqual("TextExpander_SnippetCountUnavailable", model.SnippetCountText);
            Assert.IsFalse(model.HasNoSnippets);
            Assert.IsFalse(model.HasNoMatchingSnippets);
        }

        [TestMethod]
        public void LoadingNeverRewritesTheFile()
        {
            // A hand-edited file, with a byte order mark and comments, is read as it is and left as it is.
            byte[] bytes = [.. new System.Text.UTF8Encoding(true).GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes("// mine\n" + SnippetJson.Of("!mine=value"))];
            File.WriteAllBytes(_path, bytes);

            TextExpanderViewModel model = CreateViewModel();
            model.LoadSnippets();

            Assert.AreEqual("!mine", model.Snippets.Single().Trigger);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_path));
        }

        [TestMethod]
        public void DeletingTheLastSnippetLeavesAnEmptyList()
        {
            File.WriteAllText(_path, SnippetJson.Of("!only=value"));
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.DeleteSnippet(model.Snippets.Single()));
            Assert.AreEqual(0, model.Snippets.Count);
            Assert.IsTrue(model.HasNoSnippets);
            Assert.AreEqual(0, SnippetJson.In(_path).Length);

            model.LoadSnippets();

            Assert.AreEqual(0, model.Snippets.Count);
            Assert.IsTrue(model.HasNoSnippets);
            Assert.IsFalse(model.HasSnippetError);
        }

        [TestMethod]
        public void ARowFromBeforeAReloadCanNoLongerBeEditedOrDeleted()
        {
            File.WriteAllText(_path, SnippetJson.Of("#a=A", "#b=B"));
            TextExpanderViewModel model = CreateViewModel();

            // A row a More options menu still holds after a reload, which numbers the snippets again.
            TextExpanderSnippetViewModel stale = model.Snippets.Single(row => row.Trigger == "#b");
            File.WriteAllText(_path, SnippetJson.Of("#c=C", "#a=A", "#b=B"));
            model.LoadSnippets();
            Assert.AreEqual("#a", model.Snippets.Single(row => row.Id == stale.Id).Trigger, "Its ID is now another snippet's.");

            Assert.AreEqual("TextExpander_FileChangedBeforeSave", model.ValidateRow(stale), "Edit and Duplicate check the row first.");
            Assert.AreEqual("TextExpander_FileChangedBeforeSave", model.DeleteSnippet(stale));
            Assert.AreEqual("#c=C | #a=A | #b=B", SnippetJson.Listed(_path));

            TextExpanderSnippetViewModel current = model.Snippets.Single(row => row.Trigger == "#b");
            Assert.IsNull(model.ValidateRow(current));
            Assert.IsNull(model.DeleteSnippet(current));
            Assert.AreEqual("#c=C | #a=A", SnippetJson.Listed(_path));
        }

        [TestMethod]
        public void DeletingASnippetThatIsAlreadyGoneIsNotReportedAsDeleted()
        {
            File.WriteAllText(_path, SnippetJson.Of("#a=A", "#b=B"));
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderSnippetViewModel row = model.Snippets.First();
            Assert.IsNull(model.DeleteSnippet(row));

            // The page says "Snippet deleted" only for null.
            Assert.AreEqual("TextExpander_FileChangedBeforeSave", model.DeleteSnippet(row));
            Assert.AreEqual("#b=B", SnippetJson.Listed(_path));
            Assert.AreEqual("#b", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void ValidationFindsConflictsBeforeSaveAndExcludesOnlyTheEditedEntry()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Example", "#email=you@example.com"));
            TextExpanderViewModel model = CreateViewModel();
            int id = model.Snippets.First().Id;
            Assert.IsNull(model.ValidateSnippet(id, "#sig"));
            Assert.IsNotNull(model.ValidateSnippet(null, "#sig"));
            Assert.IsNotNull(model.ValidateSnippet(id, "#email"));
            Assert.IsNull(model.ValidateSnippet(null, "#SIG"));

            File.WriteAllText(_path, SnippetJson.Of("#sig=First", "#sig=Second"));
            model.LoadSnippets();
            Assert.IsNotNull(model.ValidateSnippet(model.Snippets.Last().Id, "#sig"));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(" #bad")]
        [DataRow("#a\nb")]
        public void ValidationRejectsInvalidInputWithoutWriting(string trigger)
        {
            TextExpanderViewModel model = CreateViewModel(snippets: SnippetJson.Of("#sig=Example"));
            string path = model.SnippetsFilePath;
            byte[] before = File.ReadAllBytes(path);

            Assert.IsNotNull(model.ValidateSnippet(null, trigger));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [TestMethod]
        public void FailedSaveCanBeRetriedWithoutAFalseDuplicateConflict()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Example"));
            TextExpanderViewModel model = CreateViewModel();
            using (var lockedFile = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.IsNotNull(model.SaveSnippet(null, "#new", "New snippet"));
            }

            Assert.IsNull(model.ValidateSnippet(null, "#new"));
            Assert.IsNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual(2, model.Snippets.Count);
        }

        [TestMethod]
        public void FirstSaveKeepsTheStarterSnippets()
        {
            TextExpanderViewModel model = CreateViewModel(snippets: TextExpanderStarter.Content);
            string path = model.SnippetsFilePath;

            Assert.IsNull(model.SaveSnippet(null, "#new", "New snippet"));

            string[] saved = SnippetJson.In(path);
            Assert.AreEqual(7, saved.Length);
            CollectionAssert.AreEqual(SnippetFile.Read(System.Text.Encoding.UTF8.GetBytes(TextExpanderStarter.Content)).Snippets.Select(s => s.Trigger + "=" + s.Replacement).ToArray(), saved[..6]);
            Assert.AreEqual("#new=New snippet", saved[6]);
            Assert.AreEqual(7, model.Snippets.Count);
            Assert.AreEqual("#new", model.Snippets.Last().Trigger);
        }

        [TestMethod]
        public void ExternalFileConflictReloadsInsteadOfRestoringTheOldSnapshot()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Example"));
            TextExpanderViewModel model = CreateViewModel();
            int revision = model.SnippetRevision;
            File.WriteAllText(_path, SnippetJson.Of("#external=Updated elsewhere"));

            Assert.IsNotNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual("#external", model.Snippets.Single().Trigger);
            Assert.IsTrue(model.SnippetRevision > revision, "An open editor must be able to detect stale entry handles.");
            Assert.IsNotNull(model.ValidateSnippet(null, "#external"));
            Assert.IsNull(model.ValidateSnippet(null, "#sig"));
        }

        [TestMethod]
        public void ASaveChangesOneRowAndAnExternalEditAfterItIsStillCaught()
        {
            // PERF-5: a save used to read the file back and rebuild every row.
            File.WriteAllText(_path, SnippetJson.Of("#a=1", "#b=2", "#c=3"));
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderSnippetViewModel[] before = model.Snippets.ToArray();
            var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
            model.Snippets.CollectionChanged += (_, e) => changes.Add(e.Action);
            var shown = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
            model.FilteredSnippets.CollectionChanged += (_, e) => shown.Add(e.Action);

            Assert.IsNull(model.SaveSnippet(before[1].Id, "#b", "two"));

            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Replace, changes.Single());
            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Replace, shown.Single());
            Assert.AreSame(before[0], model.Snippets[0], "Rows that didn't change are kept.");
            Assert.AreSame(before[2], model.Snippets[2]);
            Assert.AreEqual("two", model.Snippets[1].Replacement);
            Assert.AreEqual("#a=1 | #b=two | #c=3", SnippetJson.Listed(_path));

            changes.Clear();
            Assert.IsNull(model.SaveSnippet(null, "#d", "4"));
            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Add, changes.Single());
            Assert.IsNull(model.DeleteSnippet(before[0]));
            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Remove, changes.Last());
            Assert.AreEqual("#b=two | #c=3 | #d=4", SnippetJson.Listed(_path));

            // Nothing was read back, yet an edit made elsewhere since is still caught.
            File.WriteAllText(_path, SnippetJson.Of("#elsewhere=x"));
            Assert.IsNotNull(model.SaveSnippet(null, "#e", "5"));
            Assert.AreEqual("#elsewhere", model.Snippets.Single().Trigger);
            Assert.AreEqual("#elsewhere=x", SnippetJson.Listed(_path));
        }

        [TestMethod]
        public void SearchingALongListChangesTheShownRowsInOneStep()
        {
            // PERF-1: every keystroke cleared the shown rows and added them back one by one,
            // 10,001 notifications at 10,000 snippets.
            File.WriteAllText(_path, SnippetJson.Of(Enumerable.Range(0, 10_000).Select(i => "!snip" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + "=text " + i).ToArray()));
            TextExpanderViewModel model = CreateViewModel();
            Assert.AreEqual(10_000, model.FilteredSnippets.Count);
            int notifications = 0;
            model.FilteredSnippets.CollectionChanged += (_, _) => notifications++;

            model.SearchText = "!snip0999";

            Assert.AreEqual(10, model.FilteredSnippets.Count);
            Assert.AreEqual(1, notifications);

            model.SearchText = "!snip0999";
            Assert.AreEqual(1, notifications, "The same search changes nothing.");
            model.SearchText = string.Empty;
            Assert.AreEqual(10_000, model.FilteredSnippets.Count);
            Assert.AreEqual(2, notifications);

            // The search reads each row's text without making it again.
            Assert.AreSame(model.Snippets[0].Summary, model.Snippets[0].Summary);
        }

        [TestMethod]
        [DataRow("utf-16le")]
        [DataRow("latin1")]
        public void AFileThatIsNotUtf8IsShownAsAnErrorAndNeverRewritten(string encodingName)
        {
            // A snippets file is UTF-8. One saved otherwise, such as with an ANSI é (0xE9), isn't read, so no save can
            // write U+FFFD in its place.
            System.Text.Encoding encoding = encodingName == "latin1" ? System.Text.Encoding.Latin1 : new System.Text.UnicodeEncoding(false, true);
            byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes("{ \"snippets\": [ { \"trigger\": \"#cafe\", \"replacement\": \"caf\u00E9\" } ] }")];
            File.WriteAllBytes(_path, bytes);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsTrue(model.HasSnippetError);
            Assert.AreEqual("TextExpander_SnippetError_NotJson", model.SnippetError);
            Assert.AreEqual(0, model.Snippets.Count);
            Assert.AreEqual(model.SnippetError, model.SaveSnippet(null, "#new", "New"));
            Assert.IsNotNull(model.SaveSnippet(0, "#cafe", "Changed"));
            Assert.IsNotNull(model.DeleteSnippet(new TextExpanderSnippetViewModel(0, "#cafe", "caf\u00E9")));
            Assert.IsNull(model.SuggestDuplicateTrigger(0));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_path));

            // Saved as UTF-8, it loads on Retry and edits keep the é.
            File.WriteAllText(_path, SnippetJson.Of("#cafe=caf\u00E9", "#ok=Fine"));
            model.LoadSnippets();
            Assert.IsFalse(model.HasSnippetError);
            Assert.AreEqual("caf\u00E9", model.Snippets[0].Replacement);
            Assert.IsNull(model.SaveSnippet(null, "#new", "New"));
            Assert.AreEqual("#cafe=caf\u00E9 | #ok=Fine | #new=New", SnippetJson.Listed(_path));
        }

        [TestMethod]
        [DataRow("First\nSecond\tColumn", "First\nSecond\tColumn")]
        [DataRow("First\r\nSecond\rThird", "First\nSecond\nThird")]
        [DataRow(@"C:\temp\new\nNext", @"C:\temp\new\nNext")]
        public void SummaryShowsTheReplacementAsItIsTyped(string replacement, string expected)
        {
            var snippet = new TextExpanderSnippetViewModel(0, "#sig", replacement);
            Assert.AreEqual(expected, snippet.Summary);
        }

        [TestMethod]
        public void DirtyDetectionIgnoresOnlyLineEndingDifferences()
        {
            var snippet = new TextExpanderSnippetViewModel(0, "#sig", "First\nSecond");
            Assert.IsFalse(snippet.HasChanges("#sig", "First\r\nSecond"));
            Assert.IsTrue(snippet.HasChanges("#SIG", "First\nSecond"));
            Assert.IsTrue(snippet.HasChanges("#sig", "First\nSecond "));
            Assert.IsTrue(snippet.HasChanges("#sig", "First\\nSecond"));
            Assert.IsFalse(new TextExpanderSnippetViewModel(0, string.Empty, string.Empty).HasChanges(string.Empty, string.Empty));
        }

        [TestMethod]
        public void EditorLineBreaksSaveTheSameBytesAsThePlainTextBox()
        {
            // The plain text box reported line breaks as \r. The RichEdit box's text, final
            // paragraph mark and all, comes out with \n, and must reach the file unchanged.
            string fromEditor = TextExpanderTokenText.FromEditor("Regards,\r{{prompt:Name}}\r", 25);
            Assert.AreEqual("Regards,\n{{prompt:Name}}", fromEditor);

            File.WriteAllText(_path, SnippetJson.Of("#a=one"));
            Assert.IsNull(CreateViewModel().SaveSnippet(null, "#sig", "Regards,\r{{prompt:Name}}"));
            byte[] fromPlainTextBox = File.ReadAllBytes(_path);

            File.WriteAllText(_path, SnippetJson.Of("#a=one"));
            Assert.IsNull(CreateViewModel().SaveSnippet(null, "#sig", fromEditor));
            CollectionAssert.AreEqual(fromPlainTextBox, File.ReadAllBytes(_path));
            Assert.AreEqual("#a=one | #sig=Regards,\n{{prompt:Name}}", SnippetJson.Listed(_path));
        }

        [TestMethod]
        public void ADuplicateOpensWithATriggerThatCanFireAndSavesNothing()
        {
            string library = SnippetJson.Of("!mysig=Regards", "!2mysig=Taken", "!myemail=you@example.com");
            File.WriteAllText(_path, library);
            TextExpanderViewModel model = CreateViewModel();
            string[] triggers = model.Snippets.Select(s => s.Trigger).ToArray();

            // The old name, !mysig2, could never fire: typing it completes !mysig first.
            Assert.IsFalse(CanFireAlongside("!mysig2", triggers, wordMode: false));
            Assert.AreEqual("!3mysig", model.SuggestDuplicateTrigger(model.Snippets.First().Id));
            Assert.IsTrue(CanFireAlongside("!3mysig", triggers, wordMode: false));
            Assert.AreEqual(3, model.Snippets.Count);
            Assert.AreEqual(library, File.ReadAllText(_path));

            // A copy of a copy counts on, rather than putting another number in front (QA-15).
            Assert.AreEqual("!3mysig", model.SuggestDuplicateTrigger(model.Snippets[1].Id));
        }

        [TestMethod]
        public void ACopyTriggerNeitherWaitsBehindNorBlocksAnother()
        {
            Assert.IsFalse(CanFireAlongside("!sig", SigTrigger, wordMode: false), "Same trigger.");
            Assert.IsFalse(CanFireAlongside("!sig2", SigTrigger, wordMode: false), "!sig fires first.");
            Assert.IsFalse(CanFireAlongside("!si", SigTrigger, wordMode: false), "!sig could no longer fire.");
            Assert.IsTrue(CanFireAlongside("x!sig", SigTrigger, wordMode: false), "The longer match wins at the last key.");

            Assert.AreEqual("!2sig", TextExpanderViewModel.SuggestCopyTrigger("!sig", SigTrigger, wordMode: false));
            Assert.AreEqual("2btw", TextExpanderViewModel.SuggestCopyTrigger("btw", BtwTrigger, wordMode: false));
            Assert.AreEqual("2!!", TextExpanderViewModel.SuggestCopyTrigger("!!", SymbolTrigger, wordMode: false));
        }

        [TestMethod]
        public void ACopySuggestionFollowsWaitForTheWholeWord()
        {
            // FUZZ-4: with "a" and "ab", typing any "2ab" completes "a" first, unless the engine
            // waits for the whole word; then 2ab works.
            Assert.AreEqual(string.Empty, TextExpanderViewModel.SuggestCopyTrigger("ab", AAndAb, wordMode: false));
            Assert.AreEqual("2ab", TextExpanderViewModel.SuggestCopyTrigger("ab", AAndAb, wordMode: true));

            // In word mode, a copy that a longer trigger continues with a letter doesn't fire inside it.
            Assert.IsTrue(CanFireAlongside("!2sig", SigAndLongerCopy, wordMode: true));
            Assert.IsFalse(CanFireAlongside("!2sig", SigAndLongerCopy, wordMode: false), "!2sig would fire inside !2sigs.");
            Assert.IsFalse(CanFireAlongside("!2sig", SigAndHyphenatedCopy, wordMode: true), "A hyphen ends a word, so !2sig fires there.");

            // Counting on from a copy number, past numbers that are taken.
            Assert.AreEqual("!3sample", TextExpanderViewModel.SuggestCopyTrigger("!2sample", SampleAndCopy, wordMode: false));
            Assert.AreEqual("!4sample", TextExpanderViewModel.SuggestCopyTrigger("!2sample", SampleAndTwoCopies, wordMode: false));
            Assert.AreEqual("!2sample", TextExpanderViewModel.SuggestCopyTrigger("!999sample", LastCopy, wordMode: false), "After 999 it starts again from 2.");
            Assert.AreEqual("!2007bond", TextExpanderViewModel.SuggestCopyTrigger("!007bond", Bond, wordMode: false), "A leading zero isn't a copy number.");
        }

        [TestMethod]
        public void ACopyIsNotSuggestedWhereItWouldFireInsideALongerTrigger()
        {
            // !2sig would fire inside !2sigs as it's typed, unless the engine waits for the whole word, which a hyphen ends.
            Assert.AreEqual("!3sig", TextExpanderViewModel.SuggestCopyTrigger("!sig", SigAndLongerCopy, wordMode: false));
            Assert.AreEqual("!2sig", TextExpanderViewModel.SuggestCopyTrigger("!sig", SigAndLongerCopy, wordMode: true));
            Assert.AreEqual("!3sig", TextExpanderViewModel.SuggestCopyTrigger("!sig", SigAndHyphenatedCopy, wordMode: true));

            // Every suggestion is one that adding to the triggers shows can fire alongside them.
            foreach (string[] triggers in new[] { SigTrigger, SigAndLongerCopy, SigAndHyphenatedCopy, AAndAb, SampleAndTwoCopies })
            {
                foreach (bool wordMode in new[] { false, true })
                {
                    string suggestion = TextExpanderViewModel.SuggestCopyTrigger(triggers[0], triggers, wordMode);
                    Assert.IsTrue(suggestion.Length == 0 || CanFireAlongside(suggestion, triggers, wordMode), suggestion);
                }
            }
        }

        [TestMethod]
        public void ACopySuggestionStopsAtOnceWhenNoNumberCanHelp()
        {
            // PERF-4: a short trigger inside the part every copy shares used to be checked against
            // all 998 numbers, allocating about 1 GB at 10,000 snippets.
            var triggers = Enumerable.Range(0, 10_000).Select(i => "!snip_" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)).ToList();
            triggers.Add("s");
            var clock = System.Diagnostics.Stopwatch.StartNew();

            string suggestion = TextExpanderViewModel.SuggestCopyTrigger("!snip_00000", triggers, wordMode: false);

            clock.Stop();
            Assert.AreEqual(string.Empty, suggestion);
            Assert.IsTrue(clock.ElapsedMilliseconds < 1000, $"{clock.ElapsedMilliseconds} ms");
        }

        [TestMethod]
        public void SnippetFileClonePreservesHandlesAndDoesNotMutateOriginal()
        {
            SnippetFile original = SnippetFile.Read(SnippetJson.Bytes("#one=First", "#two=Second"));
            original.Remove(original.Snippets[0].Id);
            SnippetFile clone = original.Clone();
            int id = clone.Snippets.Single().Id;
            clone.Upsert(id, "#two", "Edited");
            clone.Upsert(null, "#three", "Third");
            Assert.AreEqual(original.Snippets.Single().Id, id);
            Assert.AreEqual("Second", original.Snippets.Single().Replacement);
            Assert.AreEqual(1, SnippetFile.Read(original.ToBytes()).Snippets.Count);
            Assert.AreEqual(2, clone.Snippets.Count);
        }

        [TestMethod]
        public void ElevatedSettingsDoNotResolveReadOrWriteSnippetFiles()
        {
            const string expectedContents = "private=snippet";
            File.WriteAllText(_path, expectedContents);
            string otherFolder = Path.Combine(_directory, "different");

            TextExpanderViewModel model = CreateViewModel(isElevated: true);

            // Every entry point must refuse before touching the configured file, including the
            // search, validation, starter, retry and open-folder paths the page uses.
            string blockedMessage = model.SnippetError;
            Assert.AreEqual("TextExpander_ElevatedSnippetAccessDenied", blockedMessage);
            Assert.IsFalse(model.CanEditSnippets);
            Assert.AreEqual(string.Empty, model.SnippetsFilePath);
            Assert.IsFalse(model.HasSnippetError);
            Assert.IsFalse(model.HasNoSnippets);
            Assert.IsFalse(model.HasNoMatchingSnippets);
            Assert.AreEqual("TextExpander_SnippetCountUnavailable", model.SnippetCountText);
            Assert.AreEqual(0, model.Snippets.Count);

            model.LoadSnippets();
            model.SearchText = "private";
            Assert.AreEqual(0, model.Snippets.Count);
            Assert.AreEqual(0, model.FilteredSnippets.Count);
            Assert.AreEqual(blockedMessage, model.ValidateSnippet(null, "!new"));
            Assert.AreEqual(blockedMessage, model.SaveSnippet(null, "!new", "replacement"));
            var row = new TextExpanderSnippetViewModel(0, "!new", "replacement");
            Assert.AreEqual(blockedMessage, model.ValidateRow(row));
            Assert.AreEqual(blockedMessage, model.DeleteSnippet(row));
            Assert.AreEqual(blockedMessage, model.OpenSnippetsFile());
            Assert.IsNull(model.SuggestDuplicateTrigger(0));

            model.SnippetsPath = otherFolder;
            Assert.AreEqual(blockedMessage, model.MoveSnippetsTo(Path.Combine(otherFolder, "moved.json"), TextExpanderFileConflictChoice.ReplaceFile, out _));
            Assert.AreEqual(blockedMessage, model.UseSnippetsFile(_path, out _));
            Assert.AreEqual(_path, model.SnippetsPath);
            Assert.IsFalse(Directory.Exists(otherFolder));
            Assert.AreEqual(expectedContents, File.ReadAllText(_path));
        }

        [TestMethod]
        public void UnelevatedSettingsCanReadConfiguredSnippetFile()
        {
            File.WriteAllText(_path, SnippetJson.Of("hello=world"));
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsTrue(model.CanEditSnippets);
            Assert.AreEqual(_path, model.SnippetsFilePath);
            Assert.AreEqual("hello", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void TheCountSaysSnippetForALibraryOfOne()
        {
            // QA-14: "Showing 1 of 1 snippets".
            File.WriteAllText(_path, SnippetJson.Of("#sig=Your name"));
            TextExpanderViewModel model = CreateViewModel();
            Assert.AreEqual("1 snippet", model.SnippetCountText);

            model.SearchText = "missing";
            Assert.AreEqual("0 of 1 snippet", model.SnippetCountText);

            Assert.IsNull(model.SaveSnippet(null, "#iso", "{{isodate}}"));
            model.SearchText = string.Empty;
            Assert.AreEqual("2 snippets", model.SnippetCountText);
        }

        [TestMethod]
        public void EveryReasonASnippetCantBeSavedComesFromTheResources()
        {
            // QA-12: these were English in the Library, whatever the user's language.
            File.WriteAllText(_path, SnippetJson.Of("#sig=Your name"));
            TextExpanderViewModel model = CreateViewModel();

            Assert.AreEqual("TextExpander_Problem_EmptyTrigger", model.ValidateSnippet(null, string.Empty));
            Assert.AreEqual("TextExpander_Problem_EmptyTrigger", model.ValidateSnippet(null, "   "));
            Assert.AreEqual("TextExpander_Problem_TriggerHasLineBreak", model.ValidateSnippet(null, "#a\nb"));
            Assert.AreEqual("TextExpander_Problem_TriggerStartsWithHiddenCharacter", model.ValidateSnippet(null, "\uFEFF#a"));
            Assert.AreEqual("TextExpander_Problem_TriggerHasOuterSpace", model.ValidateSnippet(null, "#a "));
            Assert.AreEqual("Duplicate: #sig", model.ValidateSnippet(null, "#sig"));
            Assert.IsNull(model.ValidateSnippet(null, "#a"));

            // What the old text format couldn't hold is fine now.
            Assert.IsNull(model.ValidateSnippet(null, "#a=b"));
            Assert.IsNull(model.ValidateSnippet(null, "//a<<<"));

            // Saving refuses with the same words, and writes nothing.
            Assert.AreEqual("TextExpander_Problem_TriggerHasOuterSpace", model.SaveSnippet(null, " #a", "text"));
            Assert.AreEqual("#sig=Your name", SnippetJson.Listed(_path));
        }

        [TestMethod]
        public void ASaveRefusedBecauseTheFileChangedSaysSoInTheUsersLanguage()
        {
            File.WriteAllText(_path, SnippetJson.Of("#sig=Your name"));
            TextExpanderViewModel model = CreateViewModel();
            File.WriteAllText(_path, SnippetJson.Of("#sig=Your name", "#other=Changed elsewhere"));

            Assert.AreEqual("TextExpander_FileChangedBeforeSave", model.SaveSnippet(null, "#new", "text"));
            Assert.AreEqual("#sig=Your name | #other=Changed elsewhere", SnippetJson.Listed(_path), "Their change is kept.");
            Assert.AreEqual(2, model.Snippets.Count, "The list now shows the file as it is.");
        }

        [TestMethod]
        public void TheVariableMenuShowsTheTimeWhenItOpensInTheUsersLanguage()
        {
            // QA-9: the samples were made once, when the page opened, so the time went stale.
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderVariable time = model.Variables.Single(variable => variable.Token == "{{time}}");
            TextExpanderVariable clipboard = model.Variables.Single(variable => variable.Token == "{{clipboard}}");

            using (new CultureScope("en-US"))
            {
                Assert.AreEqual("TextExpander_Variable_Time = 10:40 AM", model.VariableMenuText(time, new DateTime(2026, 10, 18, 10, 40, 0)));
                Assert.AreEqual("TextExpander_Variable_Time = 11:19 AM", model.VariableMenuText(time, new DateTime(2026, 10, 18, 11, 19, 0)));
                Assert.AreEqual("TextExpander_Variable_Clipboard", model.VariableMenuText(clipboard, new DateTime(2026, 10, 18, 11, 19, 0)), "Nothing to show ahead of time.");
            }

            // F020: each sample is what the engine types, in the user's regional format.
            using (new CultureScope("de-DE"))
            {
                var now = new DateTime(2026, 10, 18, 16, 2, 0);
                CollectionAssert.AreEqual(
                    GermanVariableSamples,
                    model.Variables.Where(variable => variable.HasSample).Select(variable => variable.SampleAt(now)).ToArray());
            }

            // QA-12: every name comes from the resources.
            Assert.IsTrue(model.Variables.All(variable => variable.Name.StartsWith("TextExpander_Variable_", StringComparison.Ordinal)));
            CollectionAssert.AreEqual(VariableTokens, model.Variables.Select(variable => variable.Token).ToArray());
        }

        /// <summary>
        /// Whether <paramref name="candidate"/> could be added beside <paramref name="triggers"/>: it isn't
        /// taken, nothing fires while it is typed, and it doesn't fire while a longer trigger is typed.
        /// </summary>
        private static bool CanFireAlongside(string candidate, string[] triggers, bool wordMode)
        {
            var taken = new TextExpanderTriggerSet(triggers);
            if (taken.Contains(candidate) || TextExpanderTriggerReach.FindBlocker(candidate, taken, wordMode) is not null)
            {
                return false;
            }

            taken.Add(candidate);
            return !triggers.Any(other => other.Length > candidate.Length && TextExpanderTriggerReach.FindBlocker(other, taken, wordMode)?.Trigger == candidate);
        }

        private TextExpanderViewModel CreateViewModel(bool isElevated = false, TextExpanderSettings settings = null, TextExpanderSettings latest = null, string snippets = null)
        {
            if (snippets != null)
            {
                File.WriteAllText(_path, snippets);
            }

            settings ??= new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _path;
            if (latest != null)
            {
                // What the editor reads back after Settings changes an option.
                latest.Properties.SnippetsPath.Value = _path;
            }

            return new TextExpanderViewModel(
                EditorSettings.Store(settings, latest == null ? null : () => latest).Object,
                isElevated,
                key => key switch
                {
                    "TextExpander_SnippetCount" => "{0} of {1}",
                    "TextExpander_SnippetCount_One" => "{0} of {1} snippet",
                    "TextExpander_SnippetTotal" => "{0} snippets",
                    "TextExpander_SnippetTotal_One" => "{0} snippet",
                    "TextExpander_DuplicateTrigger" => "Duplicate: {0}",
                    "TextExpander_VariableWithSample" => "{0} = {1}",
                    _ => key,
                });
        }
    }
}

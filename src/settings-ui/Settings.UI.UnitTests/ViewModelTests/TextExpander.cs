// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class TextExpander
    {
        private static readonly string[] ExpectedTriggers = { "#sig", "#iso", "#email" };
        private static readonly string[] CloneSourceLines = { "// comment", "#one=First", "#two=Second" };
        private static readonly string[] ExpectedOriginalLines = { "// comment", "#two=Second" };
        private static readonly string[] SigTrigger = { "!sig" };
        private static readonly string[] BtwTrigger = { "btw" };
        private static readonly string[] SymbolTrigger = { "!!" };
        private static readonly string[] SigAndEmoji = { "#sig", "#emoji" };
        private static readonly string[] AAndAb = { "a", "ab" };
        private static readonly string[] SigAndLongerCopy = { "!sig", "!2sigs" };
        private static readonly string[] SigAndHyphenatedCopy = { "!sig", "!2sig-long" };
        private static readonly string[] TestAndCopy = { "!test", "!2test" };
        private static readonly string[] TestAndTwoCopies = { "!test", "!2test", "!3test" };
        private static readonly string[] LastCopy = { "!999test" };
        private static readonly string[] Bond = { "!007bond" };
        private static readonly string[] VariableTokens = { "$|$", "{{date}}", "{{time}}", "{{datetime}}", "{{isodate}}", "{{date:dddd}}", "{{clipboard}}", "{{prompt:Label}}" };
        private string _directory;
        private string _path;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "snippets.txt");
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [TestMethod]
        public void SearchMatchesTriggersAndAllReplacementLinesWithoutReordering()
        {
            File.WriteAllText(_path, "#sig=Regards,\\nYour Name\n#iso={{isodate}}\n#email=you@example.com");
            TextExpanderViewModel model = CreateViewModel();
            Assert.AreEqual(3, model.FilteredSnippets.Count);
            Assert.AreEqual("3 of 3", model.SnippetCountText);

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
            Assert.AreEqual("3 of 3", model.SnippetCountText);
        }

        [TestMethod]
        public void SettingsDefaultToCaseSensitiveMatching()
        {
            var settings = new TextExpanderSettings();

            Assert.IsTrue(settings.Properties.CaseSensitive);
            StringAssert.Contains(settings.ToJsonString(), "\"case_sensitive\":true");
        }

        [TestMethod]
        public void CaseSensitivityDefaultsOnAndShowsBothFormsWhenTurnedOff()
        {
            File.WriteAllText(_path, "!Hello=Hello");
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsTrue(model.CaseSensitive);
            Assert.AreEqual("!Hello", model.Snippets.Single().TriggerDisplay);
            Assert.IsNull(model.ValidateSnippet(null, "!HELLO", "Another"), "Different capitalization is distinct by default.");

            model.CaseSensitive = false;

            Assert.AreEqual("!hello / !HELLO", model.Snippets.Single().TriggerDisplay);
            Assert.AreEqual("Duplicate: !Hello", model.ValidateSnippet(null, "!HELLO", "Another"));
            StringAssert.Contains(model.Snippets.Single().AutomationName, "TextExpander_Row_CaseInsensitiveAutomation");
        }

        [TestMethod]
        public void ReloadAndSaveRetainCurrentSearchAndRefreshResults()
        {
            File.WriteAllText(_path, "#sig=Your name");
            TextExpanderViewModel model = CreateViewModel();
            model.SearchText = "date";
            Assert.IsTrue(model.HasNoMatchingSnippets);

            Assert.IsNull(model.SaveSnippet(null, "#iso", "{{isodate}}"));
            Assert.AreEqual("#iso", model.FilteredSnippets.Single().Trigger);
            Assert.AreEqual("date", model.SearchText);

            File.WriteAllText(_path, "#changed=New date");
            model.LoadSnippets();
            Assert.AreEqual("#changed", model.FilteredSnippets.Single().Trigger);
        }

        [TestMethod]
        public void MissingLibraryIsEmptyButReadFailureIsNotAnEmptyState()
        {
            TextExpanderViewModel model = CreateViewModel();
            Assert.IsTrue(model.HasNoSnippets);
            Assert.AreEqual("0 of 0", model.SnippetCountText);
            Assert.IsFalse(model.HasSnippetError);
            Assert.IsFalse(model.HasNoMatchingSnippets);
            Assert.IsFalse(File.Exists(_path), "Loading or offering starters must not create snippets.");

            File.WriteAllText(_path, "#sig=Example");
            using var lockedFile = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            model.LoadSnippets();
            Assert.IsTrue(model.HasSnippetError);
            Assert.AreEqual("TextExpander_SnippetCountUnavailable", model.SnippetCountText);
            Assert.IsFalse(model.HasNoSnippets);
            Assert.IsFalse(model.HasNoMatchingSnippets);
        }

        [TestMethod]
        public void ValidationFindsConflictsBeforeSaveAndExcludesOnlyTheEditedEntry()
        {
            File.WriteAllText(_path, "#sig=Example\n#email=you@example.com");
            TextExpanderViewModel model = CreateViewModel();
            int id = model.Snippets.First().Id;
            Assert.IsNull(model.ValidateSnippet(id, "#sig", "Changed"));
            Assert.IsNotNull(model.ValidateSnippet(null, "#sig", "Changed"));
            Assert.IsNotNull(model.ValidateSnippet(id, "#email", "Changed"));
            Assert.IsNull(model.ValidateSnippet(null, "#SIG", "Case-sensitive trigger"));

            File.WriteAllText(_path, "#sig=First\n#sig=Second");
            model.LoadSnippets();
            Assert.IsNotNull(model.ValidateSnippet(model.Snippets.Last().Id, "#sig", "Changed"));
        }

        [TestMethod]
        [DataRow("", "Example")]
        [DataRow("#bad=", "Example")]
        [DataRow("#sig", "First\n>>>\nLast")]
        public void ValidationRejectsInvalidInputWithoutWriting(string trigger, string replacement)
        {
            TextExpanderViewModel model = CreateViewModel();
            Assert.IsNotNull(model.ValidateSnippet(null, trigger, replacement));
            Assert.IsFalse(File.Exists(_path));
        }

        [TestMethod]
        public void FailedSaveCanBeRetriedWithoutAFalseDuplicateConflict()
        {
            File.WriteAllText(_path, "#sig=Example");
            TextExpanderViewModel model = CreateViewModel();
            using (var lockedFile = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.IsNotNull(model.SaveSnippet(null, "#new", "New snippet"));
            }

            Assert.IsNull(model.ValidateSnippet(null, "#new", "New snippet"));
            Assert.IsNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual(2, model.Snippets.Count);
        }

        [TestMethod]
        public void EngineCreatingSeedsWhileEditorIsOpenRefreshesWithoutOverwriting()
        {
            TextExpanderViewModel model = CreateViewModel();
            File.WriteAllText(_path, "#sig=Engine seed");

            Assert.IsNotNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual("#sig", model.Snippets.Single().Trigger);
            Assert.IsNotNull(model.ValidateSnippet(null, "#sig", "Conflicting starter"));
            Assert.IsNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual(2, model.Snippets.Count);
            Assert.AreEqual("Engine seed", model.Snippets[0].Replacement);
        }

        [TestMethod]
        public void ExternalFileConflictReloadsInsteadOfRestoringTheOldSnapshot()
        {
            File.WriteAllText(_path, "#sig=Example");
            TextExpanderViewModel model = CreateViewModel();
            int revision = model.SnippetRevision;
            File.WriteAllText(_path, "#external=Updated elsewhere");

            Assert.IsNotNull(model.SaveSnippet(null, "#new", "New snippet"));
            Assert.AreEqual("#external", model.Snippets.Single().Trigger);
            Assert.IsTrue(model.SnippetRevision > revision, "An open editor must be able to detect stale entry handles.");
            Assert.IsNotNull(model.ValidateSnippet(null, "#external", "Conflict"));
            Assert.IsNull(model.ValidateSnippet(null, "#sig", "No longer used"));
        }

        [TestMethod]
        public void ASaveChangesOneRowAndAnExternalEditAfterItIsStillCaught()
        {
            // PERF-5: a save used to read the file back and rebuild every row.
            File.WriteAllText(_path, "#a=1\n#b=2\n#c=3\n");
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
            Assert.AreEqual("#a=1\n#b=two\n#c=3\n", File.ReadAllText(_path));

            changes.Clear();
            Assert.IsNull(model.SaveSnippet(null, "#d", "4"));
            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Add, changes.Single());
            Assert.IsNull(model.DeleteSnippet(before[0].Id));
            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Remove, changes.Last());
            Assert.AreEqual("#b=two\n#c=3\n#d=4\n", File.ReadAllText(_path));

            // Nothing was read back, yet an edit made elsewhere since is still caught.
            File.WriteAllText(_path, "#elsewhere=x\n");
            Assert.IsNotNull(model.SaveSnippet(null, "#e", "5"));
            Assert.AreEqual("#elsewhere", model.Snippets.Single().Trigger);
            Assert.AreEqual("#elsewhere=x\n", File.ReadAllText(_path));
        }

        [TestMethod]
        public void SearchingALongListChangesTheShownRowsInOneStep()
        {
            // PERF-1: every keystroke cleared the shown rows and added them back one by one,
            // 10,001 notifications at 10,000 snippets.
            File.WriteAllText(_path, string.Concat(Enumerable.Range(0, 10_000).Select(i => "!snip" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + "=text " + i + "\n")));
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
        public void OnlyTheTriggersThatCanBeWidestAreMeasured()
        {
            Assert.AreEqual(1, TextExpanderKeycapLayout.Cells("a\u0301"), "A combining mark takes no room.");
            Assert.AreEqual(6, TextExpanderKeycapLayout.Cells("\u65E5\u672C\u8A9E"), "East Asian characters take two cells.");
            Assert.AreEqual(2, TextExpanderKeycapLayout.Cells("\U0001F600"), "So does an emoji.");

            var triggers = Enumerable.Range(1, 40).Select(length => new string('x', length)).Reverse().ToList();
            string wide = string.Concat(Enumerable.Repeat("\u65E5", 21));
            triggers.Insert(20, wide);
            IReadOnlyList<string> widest = TextExpanderKeycapLayout.WidestTriggers(triggers, count: 3);

            // 21 East Asian characters are 42 cells, wider than the 40 x's, though shorter as text.
            Assert.AreEqual(3, widest.Count);
            Assert.AreEqual(wide, widest[0]);
            Assert.AreEqual(new string('x', 40), widest[1]);
            Assert.AreEqual(new string('x', 39), widest[2]);
        }

        [TestMethod]
        [DataRow("utf-16le")]
        [DataRow("utf-16be")]
        [DataRow("utf-32le")]
        [DataRow("utf-32be")]
        public void AUtf16OrUtf32FileIsReadAndSavedInItsOwnEncoding(string encodingName)
        {
            // The engine reads these by their byte order mark; Settings used to decode them as
            // UTF-8, and a save then wrote garbage back.
            System.Text.Encoding encoding = encodingName switch
            {
                "utf-16le" => new System.Text.UnicodeEncoding(false, true),
                "utf-16be" => new System.Text.UnicodeEncoding(true, true),
                "utf-32le" => new System.Text.UTF32Encoding(false, true),
                _ => new System.Text.UTF32Encoding(true, true),
            };
            string original = "// Café ✓\r\n#sig=Grüße\r\n#emoji=👍\r\n";
            File.WriteAllBytes(_path, encoding.GetPreamble().Concat(encoding.GetBytes(original)).ToArray());
            TextExpanderViewModel model = CreateViewModel();

            CollectionAssert.AreEqual(SigAndEmoji, model.Snippets.Select(s => s.Trigger).ToArray());
            Assert.AreEqual("Grüße", model.Snippets[0].Replacement);
            Assert.AreEqual("👍", model.Snippets[1].Replacement);

            Assert.IsNull(model.SaveSnippet(null, "#new", "Ünïcødé"));

            string expected = original + "#new=Ünïcødé\r\n";
            CollectionAssert.AreEqual(encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray(), File.ReadAllBytes(_path), "Same encoding, same mark.");
            Assert.AreEqual(3, CreateViewModel().Snippets.Count, "It reads back.");
        }

        [TestMethod]
        public void SnippetEncodingIsDetectedTheWayTheEngineDetectsIt()
        {
            byte[][] samples =
            {
                new byte[] { 0xFF, 0xFE, 0, 0, 0x41, 0, 0, 0 },
                new byte[] { 0, 0, 0xFE, 0xFF, 0, 0, 0, 0x41 },
                new byte[] { 0xEF, 0xBB, 0xBF, 0x41 },
                new byte[] { 0xFF, 0xFE, 0x41, 0 },
                new byte[] { 0xFE, 0xFF, 0, 0x41 },
                new byte[] { 0x41 },
            };

            foreach (byte[] sample in samples)
            {
                // StreamReader's detection is what the engine's SnippetStore uses.
                using var reader = new StreamReader(new MemoryStream(sample), new System.Text.UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
                string engineText = reader.ReadToEnd();
                string settingsText = TextExpanderSnippetEncoding.Decode(sample, out System.Text.Encoding encoding);

                Assert.AreEqual(engineText, settingsText, BitConverter.ToString(sample));
                Assert.AreEqual("A", settingsText);
                Assert.AreEqual(reader.CurrentEncoding.WebName, encoding.WebName, BitConverter.ToString(sample));
                CollectionAssert.AreEqual(sample, TextExpanderSnippetEncoding.Encode(settingsText, encoding), "Saving gives the same bytes back.");
            }
        }

        [TestMethod]
        [DataRow(true, "\n", true)]
        [DataRow(false, "\r\n", false)]
        public void SavingPreservesBomLineEndingsCommentsAndFinalNewline(bool bom, string newline, bool finalNewline)
        {
            var encoding = new System.Text.UTF8Encoding(bom, false);
            string original = "// Keep this comment" + newline + "#sig=Example" + (finalNewline ? newline : string.Empty);
            File.WriteAllBytes(_path, encoding.GetPreamble().Concat(encoding.GetBytes(original)).ToArray());
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.SaveSnippet(null, "#new", "New"));

            string expected = "// Keep this comment" + newline + "#sig=Example" + newline + "#new=New" + (finalNewline ? newline : string.Empty);
            CollectionAssert.AreEqual(
                encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray(),
                File.ReadAllBytes(_path));
        }

        [TestMethod]
        [DataRow("First\\nSecond\\tColumn", "First\nSecond\tColumn")]
        [DataRow("First\r\nSecond\rThird", "First\nSecond\nThird")]
        public void SummaryShowsReplacementLineBreaks(string replacement, string expected)
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

            File.WriteAllText(_path, "#a=one\n");
            Assert.IsNull(CreateViewModel().SaveSnippet(null, "#sig", "Regards,\r{{prompt:Name}}"));
            byte[] fromPlainTextBox = File.ReadAllBytes(_path);

            File.WriteAllText(_path, "#a=one\n");
            Assert.IsNull(CreateViewModel().SaveSnippet(null, "#sig", fromEditor));
            CollectionAssert.AreEqual(fromPlainTextBox, File.ReadAllBytes(_path));
            Assert.AreEqual("#a=one\n#sig<<<\nRegards,\n{{prompt:Name}}\n>>>\n", File.ReadAllText(_path));
        }

        [TestMethod]
        public void ADuplicateOpensWithATriggerThatCanFireAndSavesNothing()
        {
            const string library = "!mysig=Regards\n!2mysig=Taken\n!myemail=you@example.com\n";
            File.WriteAllText(_path, library);
            TextExpanderViewModel model = CreateViewModel();
            string[] triggers = model.Snippets.Select(s => s.Trigger).ToArray();

            // The old name, !mysig2, could never fire: typing it completes !mysig first.
            Assert.IsFalse(TextExpanderViewModel.CanFireAlongside("!mysig2", triggers, wordMode: false));
            Assert.AreEqual("!3mysig", model.SuggestDuplicateTrigger(model.Snippets.First().Id));
            Assert.IsTrue(TextExpanderViewModel.CanFireAlongside("!3mysig", triggers, wordMode: false));
            Assert.AreEqual(3, model.Snippets.Count);
            Assert.AreEqual(library, File.ReadAllText(_path));

            // A copy of a copy counts on, rather than putting another number in front (QA-15).
            Assert.AreEqual("!3mysig", model.SuggestDuplicateTrigger(model.Snippets[1].Id));
        }

        [TestMethod]
        public void ACopyTriggerNeitherWaitsBehindNorBlocksAnother()
        {
            Assert.IsFalse(TextExpanderViewModel.CanFireAlongside("!sig", SigTrigger, wordMode: false), "Same trigger.");
            Assert.IsFalse(TextExpanderViewModel.CanFireAlongside("!sig2", SigTrigger, wordMode: false), "!sig fires first.");
            Assert.IsFalse(TextExpanderViewModel.CanFireAlongside("!si", SigTrigger, wordMode: false), "!sig could no longer fire.");
            Assert.IsTrue(TextExpanderViewModel.CanFireAlongside("x!sig", SigTrigger, wordMode: false), "The longer match wins at the last key.");

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
            Assert.IsTrue(TextExpanderViewModel.CanFireAlongside("!2sig", SigAndLongerCopy, wordMode: true));
            Assert.IsFalse(TextExpanderViewModel.CanFireAlongside("!2sig", SigAndLongerCopy, wordMode: false), "!2sig would fire inside !2sigs.");
            Assert.IsFalse(TextExpanderViewModel.CanFireAlongside("!2sig", SigAndHyphenatedCopy, wordMode: true), "A hyphen ends a word, so !2sig fires there.");

            // Counting on from a copy number, past numbers that are taken.
            Assert.AreEqual("!3test", TextExpanderViewModel.SuggestCopyTrigger("!2test", TestAndCopy, wordMode: false));
            Assert.AreEqual("!4test", TextExpanderViewModel.SuggestCopyTrigger("!2test", TestAndTwoCopies, wordMode: false));
            Assert.AreEqual("!2test", TextExpanderViewModel.SuggestCopyTrigger("!999test", LastCopy, wordMode: false), "After 999 it starts again from 2.");
            Assert.AreEqual("!2007bond", TextExpanderViewModel.SuggestCopyTrigger("!007bond", Bond, wordMode: false), "A leading zero isn't a copy number.");
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
            var original = TextExpanderSnippetFile.Parse(CloneSourceLines);
            original.Remove(original.Snippets[0].Id);
            TextExpanderSnippetFile clone = original.Clone();
            int id = clone.Snippets.Single().Id;
            clone.Upsert(id, "#two", "Edited");
            clone.Upsert(null, "#three", "Third");
            Assert.AreEqual(original.Snippets.Single().Id, id);
            Assert.AreEqual("Second", original.Snippets.Single().Replacement);
            CollectionAssert.AreEqual(ExpectedOriginalLines, original.ToLines());
            Assert.AreEqual(2, clone.Snippets.Count);
        }

        [TestMethod]
        public async Task ElevatedSettingsDoNotResolveReadOrWriteSnippetFiles()
        {
            const string expectedContents = "private=snippet";
            File.WriteAllText(_path, expectedContents);
            string otherFolder = Path.Combine(_directory, "different");
            int browseCalls = 0;

            TextExpanderViewModel model = CreateViewModel(
                isElevated: true,
                pickSingleFolderDialog: _ =>
                {
                    browseCalls++;
                    return Task.FromResult(otherFolder);
                });

            // Every entry point must refuse before touching the configured file, including the
            // search, validation, starter, retry and open-folder paths the page uses.
            string blockedMessage = model.SnippetError;
            Assert.AreEqual("TextExpander_ElevatedSnippetAccessDenied", blockedMessage);
            Assert.IsFalse(model.CanEditSnippets);
            Assert.IsFalse(model.IsSnippetEditingAvailable);
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
            Assert.AreEqual(blockedMessage, model.ValidateSnippet(null, "!new", "replacement"));
            Assert.AreEqual(blockedMessage, model.SaveSnippet(null, "!new", "replacement"));
            Assert.AreEqual(blockedMessage, model.DeleteSnippet(0));
            Assert.IsNull(model.SuggestDuplicateTrigger(0));

            model.SnippetsPath = otherFolder;
            Assert.IsNull(await model.PickSnippetsFolderAsync());
            Assert.AreEqual(_directory, model.SnippetsPath);
            Assert.AreEqual(0, browseCalls);
            Assert.IsFalse(Directory.Exists(otherFolder));
            Assert.AreEqual(expectedContents, File.ReadAllText(_path));
        }

        [TestMethod]
        public void UnelevatedSettingsCanReadConfiguredSnippetFile()
        {
            File.WriteAllText(_path, "hello=world");
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsTrue(model.CanEditSnippets);
            Assert.IsTrue(model.IsSnippetEditingAvailable || !model.IsEnabled);
            Assert.AreEqual(_path, model.SnippetsFilePath);
            Assert.AreEqual("hello", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public async Task ChooseFolderStartsInTheFolderInUseAndCancelKeepsIt()
        {
            File.WriteAllText(_path, "hello=world");
            string picked = null;
            var starts = new List<string>();
            TextExpanderViewModel model = CreateViewModel(pickSingleFolderDialog: start =>
            {
                starts.Add(start);
                return Task.FromResult(picked);
            });

            Assert.IsNull(await model.PickSnippetsFolderAsync());
            Assert.AreEqual(_directory, model.SnippetsPath, "Cancel keeps the folder.");

            string other = Path.Combine(_directory, "other");
            picked = other;
            TextExpanderFolderChange change = await model.PickSnippetsFolderAsync();
            Assert.IsNotNull(change);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out _));
            Assert.AreEqual(other, model.SnippetsPath);

            CollectionAssert.AreEqual(new[] { _directory, _directory }, starts, "The picker opens at the folder of the snippet file in use.");
        }

        [TestMethod]
        public void MinimumLengthToPasteAppliesOnlyToAutomatic()
        {
            TextExpanderViewModel model = CreateViewModel();
            var changed = new List<string>();
            model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            model.InjectionBackendIndex = 1;
            Assert.IsFalse(model.IsClipboardThresholdEnabled, "Always paste ignores the length.");
            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsClipboardThresholdEnabled));

            model.InjectionBackendIndex = 2;
            Assert.IsFalse(model.IsClipboardThresholdEnabled, "Always type ignores the length.");

            changed.Clear();
            model.InjectionBackendIndex = 0;
            Assert.IsTrue(model.IsClipboardThresholdEnabled, "Automatic pastes or types by length.");
            CollectionAssert.Contains(changed, nameof(TextExpanderViewModel.IsClipboardThresholdEnabled));
        }

        [TestMethod]
        public void TheCountSaysSnippetForALibraryOfOne()
        {
            // QA-14: "Showing 1 of 1 snippets".
            File.WriteAllText(_path, "#sig=Your name");
            TextExpanderViewModel model = CreateViewModel();
            Assert.AreEqual("1 of 1 snippet", model.SnippetCountText);

            model.SearchText = "missing";
            Assert.AreEqual("0 of 1 snippet", model.SnippetCountText);

            Assert.IsNull(model.SaveSnippet(null, "#iso", "{{isodate}}"));
            model.SearchText = string.Empty;
            Assert.AreEqual("2 of 2", model.SnippetCountText);
        }

        [TestMethod]
        public void EveryReasonASnippetCantBeSavedComesFromTheResources()
        {
            // QA-12: these were English in the Library, whatever the user's language.
            File.WriteAllText(_path, "#sig=Your name");
            TextExpanderViewModel model = CreateViewModel();

            Assert.AreEqual("TextExpander_Problem_EmptyTrigger", model.ValidateSnippet(null, string.Empty, "text"));
            Assert.AreEqual("TextExpander_Problem_EmptyTrigger", model.ValidateSnippet(null, "   ", "text"));
            Assert.AreEqual("TextExpander_Problem_TriggerHasLineBreak", model.ValidateSnippet(null, "#a\nb", "text"));
            Assert.AreEqual("TextExpander_Problem_TriggerStartsWithHiddenCharacter", model.ValidateSnippet(null, "\uFEFF#a", "text"));
            Assert.AreEqual("TextExpander_Problem_TriggerHasOuterSpace", model.ValidateSnippet(null, "#a ", "text"));
            Assert.AreEqual("TextExpander_Problem_TriggerHasEquals", model.ValidateSnippet(null, "#a=b", "text"));
            Assert.AreEqual("TextExpander_Problem_TriggerEndsWithBlockStart", model.ValidateSnippet(null, "#a<<<", "text"));
            Assert.AreEqual("TextExpander_Problem_TriggerStartsWithComment", model.ValidateSnippet(null, "//a", "text"));
            Assert.AreEqual("TextExpander_Problem_ReplacementHasBlockEnd", model.ValidateSnippet(null, "#a", "one\n>>>\ntwo"));
            Assert.AreEqual("Duplicate: #sig", model.ValidateSnippet(null, "#sig", "text"));
            Assert.IsNull(model.ValidateSnippet(null, "#a", "text"));

            // Saving refuses with the same words, and writes nothing.
            Assert.AreEqual("TextExpander_Problem_TriggerHasEquals", model.SaveSnippet(null, "#a=b", "text"));
            Assert.AreEqual("#sig=Your name", File.ReadAllText(_path));
        }

        [TestMethod]
        public void ASaveRefusedBecauseTheFileChangedSaysSoInTheUsersLanguage()
        {
            File.WriteAllText(_path, "#sig=Your name");
            TextExpanderViewModel model = CreateViewModel();
            File.WriteAllText(_path, "#sig=Your name\n#other=Changed elsewhere");

            Assert.AreEqual("TextExpander_FileChangedBeforeSave", model.SaveSnippet(null, "#new", "text"));
            Assert.AreEqual("#sig=Your name\n#other=Changed elsewhere", File.ReadAllText(_path), "Their change is kept.");
            Assert.AreEqual(2, model.Snippets.Count, "The list now shows the file as it is.");
        }

        [TestMethod]
        public void TheVariableMenuShowsTheTimeWhenItOpensInTheUsersLanguage()
        {
            // QA-9: the samples were made once, when the page opened, so the time went stale.
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderVariable time = model.Variables.Single(variable => variable.Token == "{{time}}");
            TextExpanderVariable clipboard = model.Variables.Single(variable => variable.Token == "{{clipboard}}");

            Assert.AreEqual("TextExpander_Variable_Time = 10:40 AM", model.VariableMenuText(time, new DateTime(2026, 10, 18, 10, 40, 0)));
            Assert.AreEqual("TextExpander_Variable_Time = 11:19 AM", model.VariableMenuText(time, new DateTime(2026, 10, 18, 11, 19, 0)));
            Assert.AreEqual("TextExpander_Variable_Clipboard", model.VariableMenuText(clipboard, new DateTime(2026, 10, 18, 11, 19, 0)), "Nothing to show ahead of time.");

            // QA-12: every name comes from the resources.
            Assert.IsTrue(model.Variables.All(variable => variable.Name.StartsWith("TextExpander_Variable_", StringComparison.Ordinal)));
            CollectionAssert.AreEqual(VariableTokens, model.Variables.Select(variable => variable.Token).ToArray());
        }

        private TextExpanderViewModel CreateViewModel(bool isElevated = false, Func<string, Task<string>> pickSingleFolderDialog = null)
        {
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _directory;
            var utils = new Mock<SettingsUtils>(new FileSystem(), null);
            utils.Setup(u => u.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
            var repository = new Mock<ISettingsRepository<GeneralSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(new GeneralSettings());
            return new TextExpanderViewModel(
                utils.Object,
                repository.Object,
                _ => 0,
                pickSingleFolderDialog,
                isElevated,
                key => key switch
                {
                    "TextExpander_SnippetCount" => "{0} of {1}",
                    "TextExpander_SnippetCount_One" => "{0} of {1} snippet",
                    "TextExpander_DuplicateTrigger" => "Duplicate: {0}",
                    "TextExpander_VariableWithSample" => "{0} = {1}",
                    _ => key,
                });
        }
    }
}

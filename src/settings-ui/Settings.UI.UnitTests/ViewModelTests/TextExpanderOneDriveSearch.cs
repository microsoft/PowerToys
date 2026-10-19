// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// The bounded OneDrive search behind the "another PC" sync setup. Most tests use a folder
    /// tree in memory, so the folder and time limits can be reached quickly.
    /// </summary>
    [TestClass]
    public class TextExpanderOneDriveSearch
    {
        private const string Root = @"C:\Users\Test\OneDrive - Contoso";
        private const string Other = @"C:\Users\Test\OneDrive";
        private const string Current = "PowerToysTextExpanderSnippets.txt";
        private const string Legacy = "snippets.txt";

        private static readonly DateTime April = new(2026, 4, 20, 8, 23, 0, DateTimeKind.Local);
        private static readonly DateTime May = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Local);
        private static readonly DateTime June = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Local);
        private static readonly DateTime September = new(2026, 9, 23, 15, 3, 0, DateTimeKind.Local);
        private static readonly DateTime October = new(2026, 10, 18, 0, 2, 0, DateTimeKind.Local);

        [TestMethod]
        public void TheCurrentNameMatchesAnywhereAndTheEarlierNameOnlyWhereThePathSaysTextExpander()
        {
            var tree = new FakeTree()
                .File(Root, Legacy, October)
                .File(Root + @"\Notes", Legacy, October)
                .File(Root + @"\Shared Notes", Legacy, October)
                .File(Root + @"\Shared Notes\powertoys-textexpander", Legacy, October)
                .File(Root + @"\Desktop\PC Software\OldTextExpander-windows-arm64", Legacy, April)
                .File(Root + @"\Power Toys\Backup", Legacy, September)
                .File(Root + @"\Archive", Current, May)
                .File(Root + @"\Text_Expander", Current, June)
                .File(Root + @"\Text_Expander", Legacy, October);

            TextExpanderSnippetSearchResult result = Search(tree, Root);

            Assert.IsTrue(result.IsComplete);
            CollectionAssert.AreEqual(
                new[]
                {
                    Root + @"\Shared Notes\powertoys-textexpander\snippets.txt",
                    Root + @"\Power Toys\Backup\snippets.txt",
                    Root + @"\Text_Expander\PowerToysTextExpanderSnippets.txt",
                    Root + @"\Archive\PowerToysTextExpanderSnippets.txt",
                    Root + @"\Desktop\PC Software\OldTextExpander-windows-arm64\snippets.txt",
                },
                result.Matches.Select(match => match.FilePath).ToArray(),
                "Newest first; a folder with both names offers the current one, however old.");
            Assert.IsTrue(result.Matches.All(match => match.Root == Root));
        }

        [TestMethod]
        [DataRow("powertoys-textexpander", true)]
        [DataRow(@"Shared Notes\powertoys-textexpander", true)]
        [DataRow("Text Expander", true)]
        [DataRow("Power_Toys", true)]
        [DataRow(@"Desktop\PC Software\OldTextExpander-windows-arm64", true)]
        [DataRow("Shared Notes", false)]
        [DataRow("Expander", false)]
        [DataRow(@"Text\Expander", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void APathHintIgnoresCaseSpacesHyphensAndUnderscores(string relativeFolder, bool hint)
        {
            Assert.AreEqual(hint, TextExpanderSnippetSearch.HasPathHint(relativeFolder));
        }

        [TestMethod]
        public void TheSearchGoesFourLevelsBelowTheAccountFolder()
        {
            var tree = new FakeTree()
                .File(Root + @"\1\2\3\4", Current, October)
                .File(Root + @"\1\2\3\4\5", Current, October);

            TextExpanderSnippetSearchResult result = Search(tree, Root);

            Assert.AreEqual(Root + @"\1\2\3\4\" + Current, result.Matches.Single().FilePath);
            CollectionAssert.DoesNotContain(tree.Listed, Root + @"\1\2\3\4\5", "A fifth level isn't even listed.");
            Assert.IsTrue(result.IsComplete);
        }

        [TestMethod]
        public void HiddenSystemDotAndLinkedFoldersAreSkippedButCloudFoldersAreNot()
        {
            var tree = new FakeTree()
                .Folder(Root + @"\.git")
                .Folder(Root + @"\Hidden", FileAttributes.Directory | FileAttributes.Hidden)
                .Folder(Root + @"\System", FileAttributes.Directory | FileAttributes.System)
                .Folder(Root + @"\Junction", FileAttributes.Directory | FileAttributes.ReparsePoint, isLink: true)
                .Folder(Root + @"\Cloud", FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.ReadOnly)
                .File(Root + @"\.git", Current, October)
                .File(Root + @"\Hidden", Current, October)
                .File(Root + @"\System", Current, October)
                .File(Root + @"\Junction", Current, October)
                .File(Root + @"\Cloud", Current, April);

            TextExpanderSnippetSearchResult result = Search(tree, Root);

            Assert.AreEqual(Root + @"\Cloud\" + Current, result.Matches.Single().FilePath, "Every OneDrive folder is a reparse point; only a link is skipped.");
            CollectionAssert.AreEqual(new[] { Root, Root + @"\Cloud" }, tree.Listed);
        }

        [TestMethod]
        public void TheSearchStopsAfterFiveThousandFoldersInAll()
        {
            var tree = new FakeTree();
            for (int i = 0; i < 6000; i++)
            {
                tree.Folder(Root + @"\f" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
            }

            // The account folder is the first folder listed, so f4998 is the 5,000th.
            tree.File(Root + @"\f4998", Current, April).File(Root + @"\f4999", Current, October);

            TextExpanderSnippetSearchResult result = Search(tree, Root, Other);

            Assert.AreEqual(TextExpanderSnippetSearch.MaxFolders, result.FoldersListed);
            Assert.AreEqual(5000, tree.Listed.Count);
            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(Root + @"\f4998\" + Current, result.Matches.Single().FilePath);
            CollectionAssert.DoesNotContain(tree.Listed, Other, "The budget is shared, so the next account isn't reached.");
        }

        [TestMethod]
        public void TheSearchStopsAfterThreeSecondsInAll()
        {
            var tree = new FakeTree()
                .File(Root + @"\a", Current, April)
                .File(Root + @"\b", Current, April)
                .File(Root + @"\c", Current, April)
                .File(Root + @"\d", Current, April);

            // Each folder takes a second.
            int checks = 0;
            TextExpanderSnippetSearchResult result = TextExpanderSnippetSearch.Search(
                new[] { Root },
                tree.List,
                () => TimeSpan.FromSeconds(checks++),
                CancellationToken.None);

            Assert.AreEqual(3, result.FoldersListed, "Listed at 0, 1 and 2 seconds; stopped at 3.");
            Assert.IsFalse(result.IsComplete);
            CollectionAssert.AreEqual(new[] { Root + @"\a\" + Current, Root + @"\b\" + Current }, result.Matches.Select(match => match.FilePath).ToArray());
        }

        [TestMethod]
        public void TheAccountsAreSearchedInTheOrderGiven()
        {
            var tree = new FakeTree()
                .File(Root + @"\Deep\Down", Current, April)
                .File(Other + @"\Top", Current, April);

            TextExpanderSnippetSearchResult result = Search(tree, Root, Other);

            CollectionAssert.AreEqual(new[] { Root, Root + @"\Deep", Root + @"\Deep\Down", Other, Other + @"\Top" }, tree.Listed, "All of the first account, breadth-first, then the next.");
            CollectionAssert.AreEqual(new[] { Root, Other }, result.Matches.Select(match => match.Root).ToArray(), "Files changed at the same time keep that order.");
        }

        [TestMethod]
        public void CancellingStopsTheSearchBetweenFolders()
        {
            var tree = new FakeTree().File(Root + @"\a", Current, April).File(Root + @"\b", Current, April);
            using var cancellation = new CancellationTokenSource();

            TextExpanderSnippetSearchResult result = TextExpanderSnippetSearch.Search(
                new[] { Root },
                folder =>
                {
                    cancellation.Cancel();
                    return tree.List(folder);
                },
                () => TimeSpan.Zero,
                cancellation.Token);

            Assert.AreEqual(1, result.FoldersListed);
            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(0, result.Matches.Count);
        }

        [TestMethod]
        public void TheRealListingTellsAJunctionFromAFolderAndListsOnlySnippetFiles()
        {
            string folder = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpanderSearch-" + Guid.NewGuid().ToString("N"));
            string junction = Path.Combine(folder, "Junction");
            Directory.CreateDirectory(Path.Combine(folder, "Plain"));
            Directory.CreateDirectory(Path.Combine(folder, "Target"));
            File.WriteAllText(Path.Combine(folder, Current), "!a=1\n");
            File.WriteAllText(Path.Combine(folder, Legacy), "!b=2\n");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "not snippets\n");
            try
            {
                // A junction needs no special rights, unlike a symbolic link.
                using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{Path.Combine(folder, "Target")}\"") { CreateNoWindow = true, UseShellExecute = false }))
                {
                    mklink.WaitForExit();
                }

                if (!Directory.Exists(junction))
                {
                    Assert.Inconclusive("Couldn't create a junction here.");
                }

                IReadOnlyList<TextExpanderFolderEntry> entries = new TextExpanderSyncEnvironment().ListFolder(folder, new[] { Current, Legacy });

                CollectionAssert.AreEquivalent(new[] { "Plain", "Target", "Junction", Current, Legacy }, entries.Select(entry => entry.Name).ToArray());
                Assert.IsTrue(entries.Single(entry => entry.Name == "Junction").IsLink);
                Assert.IsFalse(entries.Single(entry => entry.Name == "Plain").IsLink);
                Assert.IsTrue(entries.Single(entry => entry.Name == "Plain").IsDirectory);
                Assert.IsFalse(entries.Single(entry => entry.Name == Current).IsDirectory);
                Assert.AreEqual(File.GetLastWriteTime(Path.Combine(folder, Current)), entries.Single(entry => entry.Name == Current).LastWriteTime);
                Assert.AreEqual(0, new TextExpanderSyncEnvironment().ListFolder(Path.Combine(folder, "Missing"), new[] { Current }).Count, "A folder that can't be listed is empty.");
            }
            finally
            {
                if (Directory.Exists(junction))
                {
                    // Removes the link, not what it points to.
                    Directory.Delete(junction);
                }

                Directory.Delete(folder, recursive: true);
            }
        }

        private static TextExpanderSnippetSearchResult Search(FakeTree tree, params string[] roots)
            => TextExpanderSnippetSearch.Search(roots, tree.List, () => TimeSpan.Zero, CancellationToken.None);

        /// <summary>Folders and snippet files in memory, as their listings would describe them.</summary>
        private sealed class FakeTree
        {
            private readonly Dictionary<string, List<TextExpanderFolderEntry>> _folders = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Gets every folder listed, in order.</summary>
            public List<string> Listed { get; } = new();

            public FakeTree Folder(string path, FileAttributes attributes = FileAttributes.Directory, bool isLink = false)
            {
                Add(Path.GetDirectoryName(path), new TextExpanderFolderEntry(Path.GetFileName(path), true, attributes, isLink, April));
                return this;
            }

            public FakeTree File(string folder, string name, DateTime changed)
            {
                if (!string.Equals(folder, Root, StringComparison.OrdinalIgnoreCase) && !string.Equals(folder, Other, StringComparison.OrdinalIgnoreCase) && !Exists(folder))
                {
                    Folder(folder);
                }

                Add(folder, new TextExpanderFolderEntry(name, false, FileAttributes.Archive, false, changed));
                return this;
            }

            public IReadOnlyList<TextExpanderFolderEntry> List(string folder)
            {
                Listed.Add(folder);
                return _folders.TryGetValue(folder, out List<TextExpanderFolderEntry> entries) ? entries : new List<TextExpanderFolderEntry>();
            }

            private bool Exists(string folder)
                => _folders.TryGetValue(Path.GetDirectoryName(folder), out List<TextExpanderFolderEntry> siblings)
                    && siblings.Any(entry => entry.IsDirectory && string.Equals(entry.Name, Path.GetFileName(folder), StringComparison.OrdinalIgnoreCase));

            private void Add(string folder, TextExpanderFolderEntry entry)
            {
                if (!_folders.TryGetValue(folder, out List<TextExpanderFolderEntry> entries))
                {
                    _folders[folder] = entries = new List<TextExpanderFolderEntry>();

                    // A parent folder exists too, up to an account folder.
                    if (!string.Equals(folder, Root, StringComparison.OrdinalIgnoreCase) && !string.Equals(folder, Other, StringComparison.OrdinalIgnoreCase) && !Exists(folder))
                    {
                        Folder(folder);
                    }
                }

                entries.Add(entry);
            }
        }
    }
}

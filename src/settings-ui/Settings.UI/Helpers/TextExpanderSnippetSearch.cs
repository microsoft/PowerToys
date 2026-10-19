// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// The search for snippet files that the "another PC" sync setup runs when the snippets aren't
    /// where a setup puts them, for example in a folder the user made in OneDrive themselves.
    ///
    /// <para>
    /// It reads folder listings only: names, attributes and times. It never opens a file, so it
    /// can't start downloading an online-only one. It is bounded in depth, folders and time,
    /// because a OneDrive can hold far more folders than anyone would wait for.
    /// </para>
    /// </summary>
    public static class TextExpanderSnippetSearch
    {
        /// <summary>How many levels below an account's folder the search lists.</summary>
        public const int MaxDepth = 4;

        /// <summary>How many folders the search lists, across every account, before it stops.</summary>
        public const int MaxFolders = 5000;

        // Words that make a folder path likely to hold Text Expander's own "snippets.txt".
        private static readonly string[] PathHints = { "textexpander", "powertoys" };

        /// <summary>Gets how long the search runs, across every account, before it stops.</summary>
        public static TimeSpan TimeLimit { get; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Searches each of <paramref name="roots"/> in turn, breadth-first and at most
        /// <see cref="MaxDepth"/> levels down, sharing one budget of <see cref="MaxFolders"/>
        /// folders and <see cref="TimeLimit"/>. Hidden and system folders, folders whose names start
        /// with ".", and links are skipped.
        ///
        /// <para>
        /// A file with the current name matches anywhere. The name earlier versions used,
        /// "snippets.txt", is common, so it matches only in a folder whose path below the root
        /// names Text Expander or PowerToys (<see cref="HasPathHint"/>). A folder with both names
        /// offers the current one, the file the engine would read.
        /// </para>
        /// </summary>
        /// <param name="roots">The account folders, the one to search first first.</param>
        /// <param name="listFolder">Lists a folder's subfolders and snippet files.</param>
        /// <param name="elapsed">The time since the search started.</param>
        /// <param name="cancellation">Stops the search between folders.</param>
        /// <returns>The snippet files found, newest first.</returns>
        public static TextExpanderSnippetSearchResult Search(
            IReadOnlyList<string> roots,
            Func<string, IReadOnlyList<TextExpanderFolderEntry>> listFolder,
            Func<TimeSpan> elapsed,
            CancellationToken cancellation)
        {
            ArgumentNullException.ThrowIfNull(roots);
            ArgumentNullException.ThrowIfNull(listFolder);
            ArgumentNullException.ThrowIfNull(elapsed);

            var matches = new List<TextExpanderSnippetMatch>();
            int listed = 0;
            foreach (string root in roots)
            {
                // One account at a time, so the first one gets the budget first.
                var queue = new Queue<(string Folder, string Relative, int Depth)>();
                queue.Enqueue((root, string.Empty, 0));
                while (queue.Count > 0)
                {
                    if (cancellation.IsCancellationRequested || listed >= MaxFolders || elapsed() >= TimeLimit)
                    {
                        return new TextExpanderSnippetSearchResult(Newest(matches), listed, isComplete: false);
                    }

                    (string folder, string relative, int depth) = queue.Dequeue();
                    IReadOnlyList<TextExpanderFolderEntry> entries = listFolder(folder) ?? Array.Empty<TextExpanderFolderEntry>();
                    listed++;

                    TextExpanderFolderEntry? current = null;
                    TextExpanderFolderEntry? legacy = null;
                    foreach (TextExpanderFolderEntry entry in entries)
                    {
                        if (entry.IsDirectory)
                        {
                            if (depth < MaxDepth && !IsSkipped(entry))
                            {
                                queue.Enqueue((Path.Combine(folder, entry.Name), relative.Length == 0 ? entry.Name : Path.Combine(relative, entry.Name), depth + 1));
                            }
                        }
                        else if (string.Equals(entry.Name, TextExpanderSnippetFileNames.Current, StringComparison.OrdinalIgnoreCase))
                        {
                            current = entry;
                        }
                        else if (string.Equals(entry.Name, TextExpanderSnippetFileNames.Legacy, StringComparison.OrdinalIgnoreCase))
                        {
                            legacy = entry;
                        }
                    }

                    TextExpanderFolderEntry? found = current ?? (HasPathHint(relative) ? legacy : null);
                    if (found is TextExpanderFolderEntry file)
                    {
                        matches.Add(new TextExpanderSnippetMatch(root, Path.Combine(folder, file.Name), file.LastWriteTime));
                    }
                }
            }

            return new TextExpanderSnippetSearchResult(Newest(matches), listed, isComplete: true);
        }

        /// <summary>
        /// Gets a value indicating whether a folder path, relative to the account's folder, names
        /// Text Expander or PowerToys, ignoring case, spaces, hyphens and underscores, as
        /// "powertoys-textexpander" and "Text Expander" do.
        /// </summary>
        public static bool HasPathHint(string relativeFolder)
        {
            if (string.IsNullOrEmpty(relativeFolder))
            {
                return false;
            }

            string squeezed = string.Concat(relativeFolder.Where(c => c is not (' ' or '-' or '_')));
            return PathHints.Any(hint => squeezed.Contains(hint, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSkipped(TextExpanderFolderEntry folder)
            => (folder.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0
                || folder.Name.StartsWith('.')
                || folder.IsLink;

        // A stable sort, so files changed at the same time stay in the order the accounts were searched.
        private static List<TextExpanderSnippetMatch> Newest(List<TextExpanderSnippetMatch> matches)
            => matches.OrderByDescending(match => match.LastWriteTime).ToList();
    }
}

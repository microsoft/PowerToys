// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>What <see cref="TextExpanderSnippetSearch.Search"/> found, and how far it got.</summary>
    public sealed class TextExpanderSnippetSearchResult
    {
        public TextExpanderSnippetSearchResult(IReadOnlyList<TextExpanderSnippetMatch> matches, int foldersListed, bool isComplete)
        {
            Matches = matches;
            FoldersListed = foldersListed;
            IsComplete = isComplete;
        }

        /// <summary>Gets the snippet files found, newest first.</summary>
        public IReadOnlyList<TextExpanderSnippetMatch> Matches { get; }

        /// <summary>Gets the number of folders listed, across every account.</summary>
        public int FoldersListed { get; }

        /// <summary>Gets a value indicating whether every folder in reach was listed, rather than the search stopping at a limit or being cancelled.</summary>
        public bool IsComplete { get; }
    }
}

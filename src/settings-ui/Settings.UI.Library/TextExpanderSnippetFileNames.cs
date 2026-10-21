// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

#nullable enable

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// The snippet file's names, and which one a folder uses. Settings' single source for both.
    ///
    /// <para>
    /// The engine keeps the same two names and the same rule in <c>SnippetStore</c>. This file has
    /// no dependencies so that <c>TextExpander.UnitTests</c> can compile it and check that the two
    /// sides agree.
    /// </para>
    /// </summary>
    public static class TextExpanderSnippetFileNames
    {
        /// <summary>The name a new snippet file gets.</summary>
        public const string Current = "PowerToysTextExpanderSnippets.txt";

        /// <summary>
        /// The name earlier versions used. It is still read, and never renamed: a folder sync
        /// service could propagate the rename while another PC still runs an earlier version.
        /// </summary>
        public const string Legacy = "snippets.txt";

        /// <summary>
        /// Returns the snippet file in <paramref name="folder"/>: the current name if it exists,
        /// otherwise the legacy name if that exists, otherwise the current name, which is then
        /// created with the starter snippets.
        /// </summary>
        public static string ResolveInFolder(string folder, Func<string, bool> fileExists)
        {
            string current = Path.Combine(folder, Current);
            if (fileExists(current))
            {
                return current;
            }

            string legacy = Path.Combine(folder, Legacy);
            return fileExists(legacy) ? legacy : current;
        }

        /// <summary>Returns the snippet file in <paramref name="folder"/> under either name, or null if there is none.</summary>
        public static string? FindInFolder(string folder, Func<string, bool> fileExists)
        {
            string file = ResolveInFolder(folder, fileExists);
            return fileExists(file) ? file : null;
        }
    }
}

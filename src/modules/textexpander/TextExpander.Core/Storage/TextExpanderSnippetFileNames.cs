// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

#nullable enable

namespace Microsoft.PowerToys.TextExpander.Storage
{
    /// <summary>
    /// The snippet file names and folder-resolution rules shared by the editor and engine.
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
        /// created with the starter snippets. The legacy file wins when both exist only if the
        /// current file is byte-for-byte the untouched starter.
        /// </summary>
        public static string ResolveInFolder(string folder, Func<string, bool> fileExists)
        {
            string current = Path.Combine(folder, Current);
            bool currentExists = fileExists(current);
            string legacy = Path.Combine(folder, Legacy);
            bool legacyExists = fileExists(legacy);
            if (currentExists)
            {
                if (legacyExists && TextExpanderStarter.IsUntouchedStarterFile(current))
                {
                    return legacy;
                }

                return current;
            }

            return legacyExists ? legacy : current;
        }

        /// <summary>Returns the snippet file in <paramref name="folder"/> under either name, or null if there is none.</summary>
        public static string? FindInFolder(string folder, Func<string, bool> fileExists)
        {
            string file = ResolveInFolder(folder, fileExists);
            return fileExists(file) ? file : null;
        }
    }
}

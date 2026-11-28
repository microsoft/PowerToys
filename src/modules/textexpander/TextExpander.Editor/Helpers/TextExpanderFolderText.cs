// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// How the editor says where the snippets file is and shows its path. OneDrive is recognized only by comparing the path
    /// with the folders OneDrive's environment variables name; neither OneDrive nor an account is asked.
    /// </summary>
    internal static class TextExpanderFolderText
    {
        private const string Ellipsis = "\u2026";

        // HRESULT_FROM_WIN32 puts a Win32 error code in the low word, under this facility.
        private const int Win32Facility = unchecked((int)0x80070000);
        private const int FacilityMask = unchecked((int)0xFFFF0000);

        private static readonly string[] OneDriveVariables = { "%OneDrive%", "%OneDriveConsumer%", "%OneDriveCommercial%" };

        private static readonly char[] Separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        /// <summary>Gets a value indicating whether <paramref name="folder"/> is one of the OneDrive folders the environment names, or inside one.</summary>
        public static bool IsInOneDrive(string folder, Func<string, string> expandEnvironmentVariables)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            string path = Path.TrimEndingDirectorySeparator(folder);
            foreach (string variable in OneDriveVariables)
            {
                // An unset variable comes back as it was, which isn't a full path.
                string root = expandEnvironmentVariables(variable)?.Trim();
                if (string.IsNullOrEmpty(root) || !Path.IsPathFullyQualified(root))
                {
                    continue;
                }

                root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                string inside = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
                if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gets <paramref name="folder"/>'s own name, as in "Saved in {0}". A drive's root names itself.</summary>
        public static string FolderName(string folder)
        {
            string path = Path.TrimEndingDirectorySeparator(folder ?? string.Empty);
            string name = Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? path : name;
        }

        /// <summary>
        /// Shortens a full <paramref name="path"/> to at most <paramref name="maxLength"/> characters, which WinUI can't do
        /// in the middle: it keeps the root, as many leading folders as fit and the last name, with "…" for the folders in
        /// between. A path that fits, or that has no folder between its root and its last name, comes back as it is.
        /// </summary>
        public static string TrimMiddle(string path, int maxLength)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLength || !Path.IsPathFullyQualified(path))
            {
                return path ?? string.Empty;
            }

            string root = Path.GetPathRoot(path);
            string[] names = path[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (names.Length < 2)
            {
                return path;
            }

            char separator = Path.DirectorySeparatorChar;
            string tail = separator + Ellipsis + separator + names[^1];
            var head = new StringBuilder(root.TrimEnd(Separators));

            // Keeping every folder would be longer than the path itself, which doesn't fit, so at least one is dropped.
            for (int i = 0; i < names.Length - 1 && head.Length + 1 + names[i].Length + tail.Length <= maxLength; i++)
            {
                head.Append(separator).Append(names[i]);
            }

            return head.Append(tail).ToString();
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="error"/> means a folder isn't there or can't be reached yet,
        /// rather than that a file in it couldn't be used.
        /// </summary>
        public static bool IsUnreachable(Exception error)
        {
            if (error is not IOException || (error.HResult & FacilityMask) != Win32Facility)
            {
                return false;
            }

            // Path not found (3), drive not found (15), device not ready (21), network path not found (53), network
            // device or name no longer available (55, 64), network name not found (67), network location unreachable
            // (1231), and the cloud file provider, such as OneDrive, not running (362) or exited (404).
            return (error.HResult & 0xFFFF) is 3 or 15 or 21 or 53 or 55 or 64 or 67 or 362 or 404 or 1231;
        }
    }
}

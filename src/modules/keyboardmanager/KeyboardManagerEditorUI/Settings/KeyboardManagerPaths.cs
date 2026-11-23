// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace KeyboardManagerEditorUI.Settings
{
    /// <summary>
    /// The one place that decides where the editor reads and writes Keyboard Manager files
    /// (profile configs, settings.json, editorSettings.json, deviceProfiles.json,
    /// profileMetadata.json and the editor transaction lock). Every store resolves its path from
    /// <see cref="SettingsDirectory"/> on each use, so unit tests can point all of them at a
    /// temporary folder before any file is touched.
    /// Only the editor's C# stores are redirected. The native side (KeyboardMappingService and the
    /// engine library behind it, which resolve the folder through PTSettingsHelper) still reads and
    /// writes the live folder, so a test must never create a KeyboardMappingService.
    /// </summary>
    internal static class KeyboardManagerPaths
    {
        private static readonly string _productionDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager");

        private static string? _testDirectory;

        /// <summary>Gets the folder holding the Keyboard Manager files.</summary>
        public static string SettingsDirectory => _testDirectory ?? _productionDirectory;

        /// <summary>Gets a value indicating whether a test has redirected the folder.</summary>
        internal static bool IsRedirectedForTests => _testDirectory is not null;

        /// <summary>
        /// Redirects every Keyboard Manager store to <paramref name="directory"/>. For unit tests
        /// only. Refuses the user's real Keyboard Manager folder (or anything inside it), so a test
        /// can never read or overwrite live settings.
        /// </summary>
        internal static void RedirectForTests(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);

            // Path.GetFullPath also expands the 8.3 short names of the parts of the path that exist, so
            // the live folder is recognized in its short form as well.
            // Junctions and other links are not resolved.
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            string production = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_productionDirectory));
            if (full.Equals(production, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(production + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Refusing to redirect Keyboard Manager files to the live settings folder '{production}'.");
            }

            _testDirectory = full;
        }
    }
}

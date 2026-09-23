// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Peek.Common.Models;

namespace Peek.Common.Helpers
{
    public static class ShortcutHelper
    {
        /// <summary>
        /// The file extension of Windows shortcut files.
        /// </summary>
        public const string ShortcutFileExtension = ".lnk";

        /// <summary>
        /// Determines whether a path points to a Windows shortcut file.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True when the path has the shortcut file extension.</returns>
        public static bool IsShortcut(string? path) =>
            !string.IsNullOrEmpty(path) &&
            string.Equals(Path.GetExtension(path), ShortcutFileExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Resolves the target of a shortcut, using the same target the shell executes when the
        /// shortcut is opened.
        /// </summary>
        /// <param name="shortcutPath">The path of the shortcut file.</param>
        /// <returns>The target path, or null when the given path is not a shortcut, the shortcut
        /// cannot be read, or it stores no target.</returns>
        public static string? TryGetTargetPath(string? shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath) || !IsShortcut(shortcutPath))
            {
                return null;
            }

            string targetPath;

            try
            {
                // System.Link.TargetParsingPath is filled in by the shell for .lnk files.
                targetPath = PropertyStoreHelper.TryGetStringProperty(shortcutPath, PropertyKey.LinkTargetParsingPath) ?? string.Empty;
            }
            catch (Exception)
            {
                // The shortcut is damaged or the shell refused to parse it.
                return null;
            }

            targetPath = targetPath.Trim();

            if (targetPath.Length == 0)
            {
                // Advertised shortcuts (e.g. created by an MSI) and shortcuts saved by some
                // installers do not expose a target path this way.
                return null;
            }

            // Shortcuts to files under the Windows directory and shortcuts created by installers
            // can store environment variables and paths relative to the shortcut.
            targetPath = Environment.ExpandEnvironmentVariables(targetPath);

            if (!Path.IsPathRooted(targetPath))
            {
                string? shortcutDirectory = Path.GetDirectoryName(shortcutPath);

                if (string.IsNullOrEmpty(shortcutDirectory))
                {
                    return null;
                }

                try
                {
                    targetPath = Path.GetFullPath(Path.Combine(shortcutDirectory, targetPath));
                }
                catch (Exception)
                {
                    // The stored target is not a valid path.
                    return null;
                }
            }

            return targetPath;
        }

        /// <summary>
        /// Determines whether the resolved target of a shortcut exists.
        /// </summary>
        /// <param name="targetPath">The target path returned by <see cref="TryGetTargetPath"/>.</param>
        /// <returns>True when the target is an existing file or folder.</returns>
        public static bool TargetExists(string? targetPath) =>
            !string.IsNullOrEmpty(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath));
    }
}

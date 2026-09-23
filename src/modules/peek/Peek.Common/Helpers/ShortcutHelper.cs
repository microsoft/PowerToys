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
        /// The maximum number of shortcuts that are followed when a shortcut points to another
        /// shortcut.
        /// </summary>
        private const int MaxShortcutDepth = 5;

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
        /// cannot be read, or the target no longer exists.</returns>
        public static string? TryGetTargetPath(string? shortcutPath)
        {
            string? currentPath = shortcutPath;

            // Shortcuts can point at other shortcuts. Follow the chain, but stop at a fixed depth
            // so that a cycle between shortcuts cannot loop forever.
            for (int depth = 0; depth < MaxShortcutDepth; depth++)
            {
                if (currentPath == null || !IsShortcut(currentPath))
                {
                    return null;
                }

                string? targetPath = ResolveTargetPath(currentPath);

                if (targetPath == null || !IsShortcut(targetPath))
                {
                    return targetPath;
                }

                currentPath = targetPath;
            }

            return null;
        }

        /// <summary>
        /// Resolves a single shortcut to the path it stores.
        /// </summary>
        /// <param name="shortcutPath">The path of the shortcut file.</param>
        /// <returns>The stored target path, or null when it cannot be read or does not exist.</returns>
        private static string? ResolveTargetPath(string shortcutPath)
        {
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

            return File.Exists(targetPath) || Directory.Exists(targetPath) ? targetPath : null;
        }
    }
}

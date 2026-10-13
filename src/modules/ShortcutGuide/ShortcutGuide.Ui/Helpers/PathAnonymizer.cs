// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Provides a shared helper for redacting the current user's name from file paths
    /// before they are written to logs (paths under AppData/LocalAppData embed the OS
    /// username).
    /// </summary>
    internal static class PathAnonymizer
    {
        /// <summary>
        /// Replaces occurrences of the current OS username in <paramref name="path"/>
        /// with a placeholder, so logged paths do not reveal the user's identity.
        /// </summary>
        public static string Anonymize(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string userName = Environment.UserName;
            return !string.IsNullOrEmpty(userName)
                ? path.Replace(userName, "<username>", StringComparison.OrdinalIgnoreCase)
                : path;
        }
    }
}

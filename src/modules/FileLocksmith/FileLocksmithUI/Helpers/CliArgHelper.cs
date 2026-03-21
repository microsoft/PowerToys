// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace PowerToys.FileLocksmithUI.Helpers
{
    internal static class CliArgHelper
    {
        /// <summary>
        /// Returns the file/folder paths from the given command-line arguments array.
        /// Skips the executable path (index 0) and filters out the known internal <c>--elevated</c> flag (case-insensitive).
        /// All other arguments are treated as file/folder paths.
        /// </summary>
        internal static string[] GetPathsFromArgs(string[] args)
        {
            return args.Skip(1)
                       .Where(arg => !arg.Equals("--elevated", StringComparison.OrdinalIgnoreCase))
                       .ToArray();
        }
    }
}

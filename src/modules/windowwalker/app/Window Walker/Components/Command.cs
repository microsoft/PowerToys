// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

namespace WindowWalker.Components
{
    /// <summary>
    /// Command class representing a single command
    /// </summary>
    public class Command
    {
        /// <summary>
        /// Gets or sets the set of substrings to search for in the search text to figure out if the user wants this command
        /// </summary>
        public string[] SearchTexts { get; set; }

        /// <summary>
        /// Gets or sets the help tip to get displayed in the cycling display
        /// </summary>
        public string Tip { get; set; }
    }
}

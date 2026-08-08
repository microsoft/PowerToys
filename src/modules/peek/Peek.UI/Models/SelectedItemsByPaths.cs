// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Peek.UI.Models
{
    /// <summary>
    /// A CLI activation where the user supplied two or more file/folder paths. All paths are
    /// shown and navigation between them is enabled.
    /// </summary>
    public class SelectedItemsByPaths : SelectedItem
    {
        public IReadOnlyList<string> Paths { get; }

        public SelectedItemsByPaths(IReadOnlyList<string> paths)
        {
            Paths = paths;
        }

        public override bool Matches(string? path)
        {
            foreach (string p in Paths)
            {
                if (string.Equals(p, path, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

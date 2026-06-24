// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.UI.Models
{
    public class SelectedItemByPath : SelectedItem
    {
        public string Path { get; }

        public SelectedItemByPath(string path)
        {
            Path = path;
        }

        public override bool Matches(string? path)
        {
            return string.Equals(Path, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}

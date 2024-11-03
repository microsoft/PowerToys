// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Models
{
    public struct ShortcutList
    {
        public string PackageName { get; set; }

        public ShortcutCategory[] Shortcuts { get; set; }

        public string WindowFilter { get; set; }
    }
}

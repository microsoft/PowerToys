// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Models
{
    public struct ShortcutCategory
    {
        public string SectionName { get; set; }

        public Shortcut[] Properties { get; set; }
    }
}

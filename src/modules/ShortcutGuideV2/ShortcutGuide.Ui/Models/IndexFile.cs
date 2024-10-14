// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Models
{
    public struct IndexFile
    {
        public struct IndexItem
        {
            public string Filter { get; set; }

            public string[] Apps { get; set; }
        }

        public string DefaultShellName { get; set; }

        public IndexItem[] Index { get; set; }
    }
}

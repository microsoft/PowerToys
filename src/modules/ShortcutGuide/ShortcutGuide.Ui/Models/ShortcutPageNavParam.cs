// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Models
{
    internal sealed class ShortcutPageNavParam
    {
        public string AppName { get; set; } = string.Empty;

        public ShortcutFile ShortcutFile { get; set; }
    }
}

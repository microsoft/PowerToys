// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    internal static class TextReplacementTriggerKeyHelper
    {
        internal const int TabKey = 0x09;
        internal const int EnterKey = 0x0D;
        internal const int SpaceKey = 0x20;

        internal const int DefaultTriggerKey = SpaceKey;

        internal static IReadOnlySet<int> AllowedTriggerKeys { get; } = new HashSet<int>
        {
            SpaceKey,
            EnterKey,
            TabKey,
        };

        internal static bool IsAllowed(int keyCode) => AllowedTriggerKeys.Contains(keyCode);
    }
}

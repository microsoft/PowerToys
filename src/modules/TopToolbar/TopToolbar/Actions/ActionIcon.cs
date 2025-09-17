// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Actions
{
    public sealed class ActionIcon
    {
        public ActionIconType Type { get; set; } = ActionIconType.Glyph;

        public string Value { get; set; } = string.Empty;
    }
}

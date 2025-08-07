// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PeekProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(false, true, false, false, 0x20);

        public PeekProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            AlwaysRunNotElevated = new BoolProperty(true);
            CloseAfterLosingFocus = new BoolProperty(false);
            ConfirmFileDelete = new BoolProperty(true);
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public BoolProperty AlwaysRunNotElevated { get; set; }

        public BoolProperty CloseAfterLosingFocus { get; set; }

        public BoolProperty ConfirmFileDelete { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }
}

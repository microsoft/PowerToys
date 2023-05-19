// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PeekProperties
    {
        public PeekProperties()
        {
            ActivationShortcut = new HotkeySettings(false, true, false, false, 0x20);
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }
}

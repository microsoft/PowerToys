// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PastePlainProperties
    {
        public PastePlainProperties()
        {
            ActivationShortcut = new HotkeySettings(true, true, true, false, 0x56); // Ctrl+Win+Alt+V
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PeekProperties
    {
        public const double DefaultUnsupportedFileWidthPercent = 40.0;
        public const double DefaultUnsupportedFileHeightPercent = 40.0;

        public PeekProperties()
        {
            ActivationShortcut = new HotkeySettings(false, true, false, false, 0x20);
            UnsupportedFileWidthPercent = DefaultUnsupportedFileWidthPercent;
            UnsupportedFileHeightPercent = DefaultUnsupportedFileHeightPercent;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public double UnsupportedFileWidthPercent { get; set; }

        public double UnsupportedFileHeightPercent { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ClipPingProperties
    {
        public ClipPingProperties()
        {
            OverlayColor = new StringProperty("#FF0000");
        }

        public StringProperty OverlayColor { get; set; }

        public ClipPingOverlay OverlayType { get; set; }

        public string ToJsonString() => JsonSerializer.Serialize(this);
    }
}

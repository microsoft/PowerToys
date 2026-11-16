// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class RobocopyUIProperties
    {
        [JsonPropertyName("use_legacy_save_mode")]
        public BoolProperty UseLegacySaveMode { get; set; }

        public RobocopyUIProperties()
        {
            UseLegacySaveMode = new BoolProperty(false);
        }
    }
}

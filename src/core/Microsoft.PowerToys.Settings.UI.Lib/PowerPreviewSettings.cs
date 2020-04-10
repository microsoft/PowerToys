// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewSettings : BasePTModuleSettings
    {
        public PowerPreviewProperties properties { get; set; }

        public PowerPreviewSettings()
        {
            properties = new PowerPreviewProperties();
            version = "1";
            name = "_unset_";
        }

        public PowerPreviewSettings(string ptName)
        {
            properties = new PowerPreviewProperties();
            version = "1";
            name = ptName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}

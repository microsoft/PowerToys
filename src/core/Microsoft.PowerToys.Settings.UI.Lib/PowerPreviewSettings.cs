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
            this.properties = new PowerPreviewProperties();
            this.version = "1";
            this.name = "_unset_";
        }

        public PowerPreviewSettings(string ptName)
        {
            this.properties = new PowerPreviewProperties();
            this.version = "1";
            this.name = ptName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}

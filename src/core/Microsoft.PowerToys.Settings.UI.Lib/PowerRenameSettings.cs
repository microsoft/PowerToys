// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerRenameSettings : BasePTModuleSettings
    {
        public PowerRenameProperties properties { get; set; }

        public PowerRenameSettings()
        {
            this.properties = new PowerRenameProperties();
            this.version = "1";
            this.name = "_unset_";
        }

        public PowerRenameSettings(string ptName)
        {
            this.properties = new PowerRenameProperties();
            this.version = "1";
            this.name = ptName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class GeneralSettings
    {
        // Gets or sets a value indicating whether packaged.
        public bool Packaged { get; set; }

        // Gets or sets a value indicating whether run powertoys on start-up.
        public bool startup { get; set; }

        // Gets or sets a value indicating whether the powertoy elevated.
        public bool is_elevated { get; set; }

        // Gets or sets a value indicating whether powertoys should run elevated.
        public bool run_elevated { get; set; }

        // Gets or sets a value indicating whether is admin.
        public bool is_admin { get; set; }

        // Gets or sets theme Name.
        public string theme { get; set; }

        // Gets or sets system theme name.
        public string system_theme { get; set; }

        // Gets or sets powertoys version number.
        public string powertoys_version { get; set; }

        public GeneralSettings()
        {
            this.Packaged = false;
            this.startup = false;
            this.is_admin = false;
            this.is_elevated = false;
            this.theme = "system";
            this.system_theme = "light";
            this.powertoys_version = "v0.15.3";
        }

        // converts the current to a json string.
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}

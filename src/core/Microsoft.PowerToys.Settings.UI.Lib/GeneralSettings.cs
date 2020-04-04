// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class GeneralSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether packaged.
        /// </summary>
        public bool Packaged { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether run powertoys on start-up.
        /// </summary>
        public bool startup { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the powertoy elevated.
        /// </summary>
        public bool is_elevated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether powertoys should run elevated.
        /// </summary>
        public bool run_elevated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is admin.
        /// </summary>
        public bool is_admin { get; set; }

        /// <summary>
        /// Gets or sets theme Name.
        /// </summary>
        public string theme { get; set; }

        /// <summary>
        /// Gets or sets system theme name.
        /// </summary>
        public string system_theme { get; set; }

        /// <summary>
        /// Gets or sets powertoys version number.
        /// </summary>
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

        /// <summary>
        /// converts the current to a json string.
        /// </summary>
        /// <returns>returnns a json string version of the class.</returns>
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}

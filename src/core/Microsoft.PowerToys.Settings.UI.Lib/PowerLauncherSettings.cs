// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerLauncherSettings : BasePTModuleSettings
    {
        public PowerLauncherProperties properties { get; set; }

        public PowerLauncherSettings()
        {
            this.properties = new PowerLauncherProperties();
            this.version = "1";
            this.name = "_unset_";
        }
    }
}

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
            properties = new PowerLauncherProperties();
            version = "1";
            name = "_unset_";
        }
    }
}

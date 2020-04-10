// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeyboardManagerSettings : BasePTModuleSettings
    {
        public KeyboardManagerProperties properties { get; set; }

        public KeyboardManagerSettings()
        {
            this.properties = new KeyboardManagerProperties();
            this.version = "1";
            this.name = "_unset_";
        }

        public KeyboardManagerSettings(string ptName)
        {
            this.properties = new KeyboardManagerProperties();
            this.version = "1";
            this.name = ptName;
        }
    }
}

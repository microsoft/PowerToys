// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UnitTest
{
    public class BasePTSettingsTest : BasePTModuleSettings, ISettingsConfig
    {
        public BasePTSettingsTest()
        {
            Name = string.Empty;
            Version = string.Empty;
        }
    }
}

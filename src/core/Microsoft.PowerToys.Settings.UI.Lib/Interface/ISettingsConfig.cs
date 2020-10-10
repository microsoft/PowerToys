// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Lib.Interface
{
    // Common interface to be implemented by all the objects which get and store settings properties.
    public interface ISettingsConfig
    {
        string GetModuleName();

        bool UpgradeSettingsConfiguration();
    }
}

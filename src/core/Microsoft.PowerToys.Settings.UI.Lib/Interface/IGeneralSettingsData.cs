// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Lib.Interface
{
    public interface IGeneralSettingsData
    {
        bool Packaged { get; set; }

        bool Startup { get; set; }

        bool IsElevated { get; set; }

        bool RunElevated { get; set; }

        bool IsAdmin { get; set; }

        string Theme { get; set; }

        string SystemTheme { get; set; }

        string PowertoysVersion { get; set; }

        string CustomActionName { get; set; }

        EnabledModules Enabled { get; set; }

        bool AutoDownloadUpdates { get; set; }

        string ToJsonString();
    }
}

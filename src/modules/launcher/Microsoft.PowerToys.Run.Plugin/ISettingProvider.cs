// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Controls;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Run.Plugin
{
    public interface ISettingProvider
    {
        Control CreateSettingPanel();

        void UpdateSettings(PowerLauncherSettings settings);
    }
}

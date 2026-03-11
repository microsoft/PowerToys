// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Event arguments for settings changes. </summary>
public class SettingsChangedEventArgs : EventArgs
{
    public SettingsModel NewSettingsModel { get; set; }

    public SettingsChangedEventArgs(SettingsModel newSettingsModel)
    {
        NewSettingsModel = newSettingsModel;
    }
}

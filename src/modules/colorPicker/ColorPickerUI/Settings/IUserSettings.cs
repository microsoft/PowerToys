// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using ColorPicker.Common;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace ColorPicker.Settings
{
    public interface IUserSettings
    {
        SettingItem<string> ActivationShortcut { get; }

        SettingItem<bool> ChangeCursor { get; }

        SettingItem<string> CopiedColorRepresentation { get; set; }

        SettingItem<string> CopiedColorRepresentationFormat { get; set; }

        SettingItem<ColorPickerActivationAction> ActivationAction { get; }

        RangeObservableCollection<string> ColorHistory { get; }

        SettingItem<int> ColorHistoryLimit { get; }

        ObservableCollection<System.Collections.Generic.KeyValuePair<string, string>> VisibleColorFormats { get; }

        SettingItem<bool> ShowColorName { get; }

        void SendSettingsTelemetry();
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    [WinRT.GeneratedBindableCustomProperty]
    public sealed partial class PluginAdditionalOptionComboBoxItem
    {
        public PluginAdditionalOptionComboBoxItem(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public string Value { get; }

        public override string ToString() => Label;
    }
}

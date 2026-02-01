// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public sealed class HotkeyLauncherActions
    {
        [JsonPropertyName("value")]
        public ObservableCollection<HotkeyLauncherAction> Value { get; set; } = new ObservableCollection<HotkeyLauncherAction>();
    }
}

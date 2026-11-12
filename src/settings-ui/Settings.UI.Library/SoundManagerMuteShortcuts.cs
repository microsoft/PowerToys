// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // The programs that have a shortcut of their own, which silence them.
    public sealed class SoundManagerMuteShortcuts
    {
        private readonly ObservableCollection<SoundManagerMuteShortcut> _value = [];

        [JsonPropertyName("value")]
        public ObservableCollection<SoundManagerMuteShortcut> Value
        {
            get => _value;
            init => _value = value ?? [];
        }
    }
}

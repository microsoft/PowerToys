// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class TextReplacementDataModel : IJsonOnDeserialized
    {
        private const int SpaceKey = 0x20;

        private int? _triggerKey;

        [JsonPropertyName("trigger")]
        public string Trigger { get; set; } = string.Empty;

        [JsonPropertyName("unicodeText")]
        public string NewRemapString { get; set; } = string.Empty;

        [JsonPropertyName("triggerKey")]
        public int TriggerKey
        {
            get => _triggerKey ?? SpaceKey;
            set => _triggerKey = value;
        }

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (_triggerKey is not null)
            {
                return;
            }

            _triggerKey = SpaceKey;
            if (Trigger.Length > 1 && Trigger.EndsWith(' '))
            {
                Trigger = Trigger[..^1];
            }
        }
    }
}

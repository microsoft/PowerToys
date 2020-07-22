// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.Telemetry;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewProperties
    {
        private bool enableSvg = true;

        [JsonPropertyName("svg-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableSvg
        {
            get => this.enableSvg;
            set
            {
                if (value != this.enableSvg)
                {
                    LogTelemetryEvent(value);
                    this.enableSvg = value;
                }
            }
        }

        private bool enableMd = true;

        [JsonPropertyName("md-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableMd
        {
            get => this.enableMd;
            set
            {
                if (value != this.enableMd)
                {
                    LogTelemetryEvent(value);
                    this.enableMd = value;
                }
            }
        }

        public PowerPreviewProperties()
        {

        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        private void LogTelemetryEvent(bool value, [CallerMemberName] string propertyName = null)
        {
            var dataEvent = new SettingsEnabledEvent()
            {
                Value = value,
                Name = propertyName,
            };
            PowerToysTelemetry.Log.WriteEvent(dataEvent);
        }
    }
}

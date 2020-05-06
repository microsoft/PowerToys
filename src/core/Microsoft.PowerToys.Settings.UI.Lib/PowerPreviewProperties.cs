// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.Telemetry;
using Microsoft.PowerToys.Telemetry;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewProperties
    {

        [JsonPropertyName("svg-previewer-toggle-setting")]
        private BoolProperty EnableSvgInner { get; set; }

        public bool EnableSvg
        {
            get => this.EnableSvgInner.Value;
            set
            {
                if (value != this.EnableSvgInner.Value)
                {
                    LogTelemetryEvent(value);
                    this.EnableSvgInner.Value = value;
                }
            }
        }

        [JsonPropertyName("md-previewer-toggle-setting")]
        private BoolProperty EnableMdInner { get; set; }

        public bool EnableMd
        {
            get => this.EnableMdInner.Value;
            set
            {
                if (value != this.EnableMdInner.Value)
                {
                    LogTelemetryEvent(value);
                    this.EnableMdInner.Value = value;
                }
            }
        }

        public PowerPreviewProperties()
        {
            EnableSvgInner = new BoolProperty(true);
            EnableMdInner = new BoolProperty(true);
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

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.Telemetry;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerPreviewProperties
    {
        private bool enableSvgPreview = true;

        [JsonPropertyName("svg-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableSvgPreview
        {
            get => enableSvgPreview;
            set
            {
                if (value != enableSvgPreview)
                {
                    LogTelemetryEvent(value);
                    enableSvgPreview = value;
                }
            }
        }

        private bool enableSvgThumbnail = true;

        [JsonPropertyName("svg-thumbnail-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableSvgThumbnail
        {
            get => enableSvgThumbnail;
            set
            {
                if (value != enableSvgThumbnail)
                {
                    LogTelemetryEvent(value);
                    enableSvgThumbnail = value;
                }
            }
        }

        private bool enableMdPreview = true;

        [JsonPropertyName("md-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableMdPreview
        {
            get => enableMdPreview;
            set
            {
                if (value != enableMdPreview)
                {
                    LogTelemetryEvent(value);
                    enableMdPreview = value;
                }
            }
        }

        private bool enablePdfPreview = true;

        [JsonPropertyName("pdf-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnablePdfPreview
        {
            get => enablePdfPreview;
            set
            {
                if (value != enablePdfPreview)
                {
                    LogTelemetryEvent(value);
                    enablePdfPreview = value;
                }
            }
        }

        private bool enablePdfThumbnail = true;

        [JsonPropertyName("pdf-thumbnail-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnablePdfThumbnail
        {
            get => enablePdfThumbnail;
            set
            {
                if (value != enablePdfThumbnail)
                {
                    LogTelemetryEvent(value);
                    enablePdfThumbnail = value;
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

        private static void LogTelemetryEvent(bool value, [CallerMemberName] string propertyName = null)
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

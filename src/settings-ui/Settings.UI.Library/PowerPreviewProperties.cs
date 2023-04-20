// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.Telemetry;
using Microsoft.PowerToys.Telemetry;
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerPreviewProperties
    {
        public const string DefaultStlThumbnailColor = "#FFC924";
        public const int DefaultMonacoMaxFileSize = 50;
        public const int DefaultSvgBackgroundColorMode = (int)SvgPreviewColorMode.Default;
        public const string DefaultSvgBackgroundSolidColor = "#FFFFFF";
        public const int DefaultSvgBackgroundCheckeredShade = (int)SvgPreviewCheckeredShade.Light;

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

        [JsonPropertyName("svg-previewer-background-color-mode")]
        public IntProperty SvgBackgroundColorMode { get; set; }

        [JsonPropertyName("svg-previewer-background-solid-color")]
        public StringProperty SvgBackgroundSolidColor { get; set; }

        [JsonPropertyName("svg-previewer-background-checkered-shade")]
        public IntProperty SvgBackgroundCheckeredShade { get; set; }

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

        private bool enableMonacoPreview = true;

        [JsonPropertyName("monaco-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableMonacoPreview
        {
            get => enableMonacoPreview;
            set
            {
                if (value != enableMonacoPreview)
                {
                    LogTelemetryEvent(value);
                    enableMonacoPreview = value;
                }
            }
        }

        private bool monacoPreviewWordWrap = true;

        [JsonPropertyName("monaco-previewer-toggle-setting-word-wrap")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableMonacoPreviewWordWrap
        {
            get => monacoPreviewWordWrap;
            set
            {
                if (value != monacoPreviewWordWrap)
                {
                    LogTelemetryEvent(value);
                    monacoPreviewWordWrap = value;
                }
            }
        }

        private bool monacoPreviewTryFormat;

        [JsonPropertyName("monaco-previewer-toggle-try-format")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool MonacoPreviewTryFormat
        {
            get => monacoPreviewTryFormat;
            set
            {
                if (value != monacoPreviewTryFormat)
                {
                    LogTelemetryEvent(value);
                    monacoPreviewTryFormat = value;
                }
            }
        }

        [JsonPropertyName("monaco-previewer-max-file-size")]
        public IntProperty MonacoPreviewMaxFileSize { get; set; }

        private bool enablePdfPreview;

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

        private bool enablePdfThumbnail;

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

        private bool enableGcodePreview = true;

        [JsonPropertyName("gcode-previewer-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableGcodePreview
        {
            get => enableGcodePreview;
            set
            {
                if (value != enableGcodePreview)
                {
                    LogTelemetryEvent(value);
                    enableGcodePreview = value;
                }
            }
        }

        private bool enableGcodeThumbnail = true;

        [JsonPropertyName("gcode-thumbnail-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableGcodeThumbnail
        {
            get => enableGcodeThumbnail;
            set
            {
                if (value != enableGcodeThumbnail)
                {
                    LogTelemetryEvent(value);
                    enableGcodeThumbnail = value;
                }
            }
        }

        private bool enableStlThumbnail = true;

        [JsonPropertyName("stl-thumbnail-toggle-setting")]
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableStlThumbnail
        {
            get => enableStlThumbnail;
            set
            {
                if (value != enableStlThumbnail)
                {
                    LogTelemetryEvent(value);
                    enableStlThumbnail = value;
                }
            }
        }

        [JsonPropertyName("stl-thumbnail-color-setting")]
        public StringProperty StlThumbnailColor { get; set; }

        public PowerPreviewProperties()
        {
            SvgBackgroundColorMode = new IntProperty(DefaultSvgBackgroundColorMode);
            SvgBackgroundSolidColor = new StringProperty(DefaultSvgBackgroundSolidColor);
            SvgBackgroundCheckeredShade = new IntProperty(DefaultSvgBackgroundCheckeredShade);
            StlThumbnailColor = new StringProperty(DefaultStlThumbnailColor);
            MonacoPreviewMaxFileSize = new IntProperty(DefaultMonacoMaxFileSize);
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

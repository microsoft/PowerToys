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
    public class EnabledModules
    {
        public EnabledModules()
        {
        }

        private bool fancyZones = true;

        [JsonPropertyName("FancyZones")]
        public bool FancyZones
        {
            get => fancyZones;
            set
            {
                if (fancyZones != value)
                {
                    LogTelemetryEvent(value);
                    fancyZones = value;
                }
            }
        }

        private bool imageResizer = true;

        [JsonPropertyName("Image Resizer")]
        public bool ImageResizer
        {
            get => imageResizer;
            set
            {
                if (imageResizer != value)
                {
                    LogTelemetryEvent(value);
                    imageResizer = value;
                }
            }
        }

        private bool fileExplorerPreview = true;

        [JsonPropertyName("File Explorer Preview")]
        public bool FileExplorerPreview
        {
            get => fileExplorerPreview;
            set
            {
                if (fileExplorerPreview != value)
                {
                    LogTelemetryEvent(value);
                    fileExplorerPreview = value;
                }
            }
        }

        private bool shortcutGuide = true;

        [JsonPropertyName("Shortcut Guide")]
        public bool ShortcutGuide
        {
            get => shortcutGuide;
            set
            {
                if (shortcutGuide != value)
                {
                    LogTelemetryEvent(value);
                    shortcutGuide = value;
                }
            }
        }

        private bool videoConference = true;

        [JsonPropertyName("Video Conference")]
        public bool VideoConference
        {
            get => this.videoConference;
            set
            {
                if (this.videoConference != value)
                {
                    LogTelemetryEvent(value);
                    this.videoConference = value;
                }
            }
        }

        private bool powerRename = true;

        public bool PowerRename
        {
            get => powerRename;
            set
            {
                if (powerRename != value)
                {
                    LogTelemetryEvent(value);
                    powerRename = value;
                }
            }
        }

        private bool keyboardManager = true;

        [JsonPropertyName("Keyboard Manager")]
        public bool KeyboardManager
        {
            get => keyboardManager;
            set
            {
                if (keyboardManager != value)
                {
                    LogTelemetryEvent(value);
                    keyboardManager = value;
                }
            }
        }

        private bool powerLauncher = true;

        [JsonPropertyName("PowerToys Run")]
        public bool PowerLauncher
        {
            get => powerLauncher;
            set
            {
                if (powerLauncher != value)
                {
                    LogTelemetryEvent(value);
                    powerLauncher = value;
                }
            }
        }

        private bool colorPicker = true;

        [JsonPropertyName("ColorPicker")]
        public bool ColorPicker
        {
            get => colorPicker;
            set
            {
                if (colorPicker != value)
                {
                    LogTelemetryEvent(value);
                    colorPicker = value;
                }
            }
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        private static void LogTelemetryEvent(bool value, [CallerMemberName] string moduleName = null)
        {
            var dataEvent = new SettingsEnabledEvent()
            {
                Value = value,
                Name = moduleName,
            };
            PowerToysTelemetry.Log.WriteEvent(dataEvent);
        }
    }
}

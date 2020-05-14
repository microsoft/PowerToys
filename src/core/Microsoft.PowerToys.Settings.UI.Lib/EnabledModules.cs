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
    public class EnabledModules
    {
        public EnabledModules()
        {
        }

        private bool fancyZones = true;

        [JsonPropertyName("FancyZones")]
        public bool FancyZones
        {
            get => this.fancyZones;
            set
            {
                if (this.fancyZones != value)
                {
                    LogTelemetryEvent(value);
                    this.fancyZones = value;
                }
            }
        }

        private bool imageResizer = true;

        [JsonPropertyName("Image Resizer")]
        public bool ImageResizer
        {
            get => this.imageResizer;
            set
            {
                if (this.imageResizer != value)
                {
                    LogTelemetryEvent(value);
                    this.imageResizer = value;
                }
            }
        }

        private bool fileExplorerPreview = true;

        [JsonPropertyName("File Explorer Preview")]
        public bool FileExplorerPreview
        {
            get => this.fileExplorerPreview;
            set
            {
                if (this.fileExplorerPreview != value)
                {
                    LogTelemetryEvent(value);
                    this.fileExplorerPreview = value;
                }
            }
        }

        private bool shortcutGuide = true;

        [JsonPropertyName("Shortcut Guide")]
        public bool ShortcutGuide
        {
            get => this.shortcutGuide;
            set
            {
                if (this.shortcutGuide != value)
                {
                    LogTelemetryEvent(value);
                    this.shortcutGuide = value;
                }
            }
        }

        private bool powerRename = true;

        public bool PowerRename
        {
            get => this.powerRename;
            set
            {
                if (this.powerRename != value)
                {
                    LogTelemetryEvent(value);
                    this.powerRename = value;
                }
            }
        }

        private bool keyboardManager = true;
        [JsonPropertyName("Keyboard Manager")]
        public bool KeyboardManager
        {
            get => this.keyboardManager;
            set
            {
                if (this.keyboardManager != value)
                {
                    LogTelemetryEvent(value);
                    this.keyboardManager = value;
                }
            }
        }

        private bool powerLauncher = true;

     	[JsonPropertyName("Run")]
        public bool PowerLauncher
        {
            get => this.powerLauncher;
            set
            {
                if (this.powerLauncher != value)
                {
                    LogTelemetryEvent(value);
                    this.powerLauncher = value;
                }
            }
}

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        private void LogTelemetryEvent(bool value, [CallerMemberName] string moduleName = null )
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
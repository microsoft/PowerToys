// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.Telemetry;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class EnabledModules
    {
        private Action notifyEnabledChangedAction;

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
                    NotifyChange();
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
                    NotifyChange();
                }
            }
        }

        private bool videoConference; // defaulting to off https://github.com/microsoft/PowerToys/issues/14507

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
                    NotifyChange();
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
                    NotifyChange();
                }
            }
        }

        private bool awake;

        [JsonPropertyName("Awake")]
        public bool Awake
        {
            get => awake;
            set
            {
                if (awake != value)
                {
                    LogTelemetryEvent(value);
                    awake = value;
                }
            }
        }

        private bool findMyMouse = true;

        [JsonPropertyName("FindMyMouse")]
        public bool FindMyMouse
        {
            get => findMyMouse;
            set
            {
                if (findMyMouse != value)
                {
                    LogTelemetryEvent(value);
                    findMyMouse = value;
                }
            }
        }

        private bool mouseHighlighter = true;

        [JsonPropertyName("MouseHighlighter")]
        public bool MouseHighlighter
        {
            get => mouseHighlighter;
            set
            {
                if (mouseHighlighter != value)
                {
                    LogTelemetryEvent(value);
                    mouseHighlighter = value;
                }
            }
        }

        private bool alwaysOnTop = true;

        [JsonPropertyName("AlwaysOnTop")]
        public bool AlwaysOnTop
        {
            get => alwaysOnTop;
            set
            {
                if (alwaysOnTop != value)
                {
                    LogTelemetryEvent(value);
                    alwaysOnTop = value;
                }
            }
        }

        private bool mousePointerCrosshairs = true;

        [JsonPropertyName("MousePointerCrosshairs")]
        public bool MousePointerCrosshairs
        {
            get => mousePointerCrosshairs;
            set
            {
                if (mousePointerCrosshairs != value)
                {
                    LogTelemetryEvent(value);
                    mousePointerCrosshairs = value;
                }
            }
        }

        private bool powerAccent;

        [JsonPropertyName("QuickAccent")]
        public bool PowerAccent
        {
            get => powerAccent;
            set
            {
                if (powerAccent != value)
                {
                    LogTelemetryEvent(value);
                    powerAccent = value;
                }
            }
        }

        private bool powerOCR = true;

        [JsonPropertyName("TextExtractor")]
        public bool PowerOCR
        {
            get => powerOCR;
            set
            {
                if (powerOCR != value)
                {
                    LogTelemetryEvent(value);
                    powerOCR = value;
                    NotifyChange();
                }
            }
        }

        private bool measureTool = true;

        [JsonPropertyName("Measure Tool")]
        public bool MeasureTool
        {
            get => measureTool;
            set
            {
                if (measureTool != value)
                {
                    LogTelemetryEvent(value);
                    measureTool = value;
                    NotifyChange();
                }
            }
        }

        private bool hosts = true;

        [JsonPropertyName("Hosts")]
        public bool Hosts
        {
            get => hosts;
            set
            {
                if (hosts != value)
                {
                    LogTelemetryEvent(value);
                    hosts = value;
                    NotifyChange();
                }
            }
        }

        private bool fileLocksmith = true;

        [JsonPropertyName("File Locksmith")]
        public bool FileLocksmith
        {
            get => fileLocksmith;
            set
            {
                if (fileLocksmith != value)
                {
                    LogTelemetryEvent(value);
                    fileLocksmith = value;
                }
            }
        }

        private void NotifyChange()
        {
            notifyEnabledChangedAction?.Invoke();
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

        internal void AddEnabledModuleChangeNotification(Action callBack)
        {
            notifyEnabledChangedAction = callBack;
        }
    }
}

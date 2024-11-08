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

        // Default values for enabled modules should match their expected "enabled by default" values.
        // Otherwise, a run of DSC on clean settings will not match the expected default result.
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
        public bool PowerPreview
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

        private bool keyboardManager; // defaulting to off

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

        private bool cropAndLock = true;

        [JsonPropertyName("CropAndLock")]
        public bool CropAndLock
        {
            get => cropAndLock;
            set
            {
                if (cropAndLock != value)
                {
                    LogTelemetryEvent(value);
                    cropAndLock = value;
                    NotifyChange();
                }
            }
        }

        private bool awake = true;

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

        private bool mouseWithoutBorders; // defaulting to off

        [JsonPropertyName("MouseWithoutBorders")]
        public bool MouseWithoutBorders
        {
            get => mouseWithoutBorders;
            set
            {
                if (mouseWithoutBorders != value)
                {
                    LogTelemetryEvent(value);
                    mouseWithoutBorders = value;
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

        private bool mouseJump; // defaulting to off

        [JsonPropertyName("MouseJump")]
        public bool MouseJump
        {
            get => mouseJump;
            set
            {
                if (mouseJump != value)
                {
                    LogTelemetryEvent(value);
                    mouseJump = value;
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

        private bool mousePointerCrosshairs; // defaulting to off

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

        private bool powerAccent; // defaulting to off

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

        private bool powerOCR; // defaulting to off

        [JsonPropertyName("TextExtractor")]
        public bool PowerOcr
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

        private bool advancedPaste = true;

        [JsonPropertyName("AdvancedPaste")]
        public bool AdvancedPaste
        {
            get => advancedPaste;
            set
            {
                if (advancedPaste != value)
                {
                    LogTelemetryEvent(value);
                    advancedPaste = value;
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

        private bool peek = true;

        [JsonPropertyName("Peek")]
        public bool Peek
        {
            get => peek;
            set
            {
                if (peek != value)
                {
                    LogTelemetryEvent(value);
                    peek = value;
                }
            }
        }

        private bool registryPreview = true;

        [JsonPropertyName("RegistryPreview")]
        public bool RegistryPreview
        {
            get => registryPreview;
            set
            {
                if (registryPreview != value)
                {
                    LogTelemetryEvent(value);
                    registryPreview = value;
                }
            }
        }

        private bool cmdNotFound = true;

        [JsonPropertyName("CmdNotFound")]
        public bool CmdNotFound
        {
            get => cmdNotFound;
            set
            {
                if (cmdNotFound != value)
                {
                    LogTelemetryEvent(value);
                    cmdNotFound = value;
                    NotifyChange();
                }
            }
        }

        private bool environmentVariables = true;

        [JsonPropertyName("EnvironmentVariables")]
        public bool EnvironmentVariables
        {
            get => environmentVariables;
            set
            {
                if (environmentVariables != value)
                {
                    LogTelemetryEvent(value);
                    environmentVariables = value;
                }
            }
        }

        private bool newPlus;

        [JsonPropertyName("NewPlus")] // This key must match newplus::constants::non_localizable
        public bool NewPlus
        {
            get => newPlus;
            set
            {
                if (newPlus != value)
                {
                    LogTelemetryEvent(value);
                    newPlus = value;
                }
            }
        }

        private bool workspaces = true;

        [JsonPropertyName("Workspaces")]
        public bool Workspaces
        {
            get => workspaces;
            set
            {
                if (workspaces != value)
                {
                    LogTelemetryEvent(value);
                    workspaces = value;
                    NotifyChange();
                }
            }
        }

        private bool zoomIt;

        [JsonPropertyName("ZoomIt")]
        public bool ZoomIt
        {
            get => zoomIt;
            set
            {
                if (zoomIt != value)
                {
                    LogTelemetryEvent(value);
                    zoomIt = value;
                    NotifyChange();
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

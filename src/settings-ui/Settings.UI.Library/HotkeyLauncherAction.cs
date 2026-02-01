// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public sealed class HotkeyLauncherAction : Observable
    {
        private int _id;
        private HotkeySettings _hotkey;
        private string _actionPath = string.Empty;
        private string _arguments = string.Empty;
        private string _workingDirectory = string.Empty;

        public HotkeyLauncherAction()
        {
            _hotkey = new HotkeySettings();
        }

        [JsonPropertyName("id")]
        public int Id
        {
            get => _id;
            set => Set(ref _id, value);
        }

        [JsonPropertyName("hotkey")]
        public HotkeySettings Hotkey
        {
            get => _hotkey;
            set
            {
                if (_hotkey != value)
                {
                    _hotkey = value ?? new HotkeySettings();
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("action_path")]
        public string ActionPath
        {
            get => _actionPath;
            set => Set(ref _actionPath, value ?? string.Empty);
        }

        [JsonPropertyName("arguments")]
        public string Arguments
        {
            get => _arguments;
            set => Set(ref _arguments, value ?? string.Empty);
        }

        [JsonPropertyName("working_directory")]
        public string WorkingDirectory
        {
            get => _workingDirectory;
            set => Set(ref _workingDirectory, value ?? string.Empty);
        }
    }
}

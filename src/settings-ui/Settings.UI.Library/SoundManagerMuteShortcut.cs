// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // A program that has a shortcut of its own, which silences it and brings it back.
    public sealed class SoundManagerMuteShortcut : Observable
    {
        private string _program = string.Empty;
        private string _name = string.Empty;
        private string _processPath = string.Empty;
        private HotkeySettings _shortcut = new();

        // The name of the executable file, which is what tells one program from another. The path is left out, because a program can have more than one process.
        [JsonPropertyName("program")]
        public string Program
        {
            get => _program;
            set => Set(ref _program, value ?? string.Empty);
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        // The place of the executable file of the program. It is only used to draw the icon, because the name does not say where it is.
        [JsonPropertyName("processPath")]
        public string ProcessPath
        {
            get => _processPath;
            set => Set(ref _processPath, value ?? string.Empty);
        }

        [JsonPropertyName("shortcut")]
        public HotkeySettings Shortcut
        {
            get => _shortcut;
            set => Set(ref _shortcut, value ?? new());
        }
    }
}

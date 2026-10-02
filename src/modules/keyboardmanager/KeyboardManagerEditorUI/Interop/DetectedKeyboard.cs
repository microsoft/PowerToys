// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Interop
{
    /// <summary>A physical keyboard discovered via Raw Input, for the per-keyboard profile UI.</summary>
    public sealed class DetectedKeyboard
    {
        /// <summary>Normalized RIDI_DEVICENAME (stable prefix); the key matched by the engine.</summary>
        public string DevicePath { get; init; } = string.Empty;

        /// <summary>Human-readable name (HID product string, else VID/PID, else a short path).</summary>
        public string DisplayName { get; init; } = string.Empty;
    }
}

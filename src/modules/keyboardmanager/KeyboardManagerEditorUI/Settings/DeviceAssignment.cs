// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Settings
{
    /// <summary>A saved keyboard→profile assignment (with a friendly name for display).</summary>
    public sealed class DeviceAssignment
    {
        public string Device { get; set; } = string.Empty;

        public string Profile { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}

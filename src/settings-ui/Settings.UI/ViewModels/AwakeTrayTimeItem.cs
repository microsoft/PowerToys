// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A time shortcut shown in the Awake tray menu.
    /// </summary>
    public sealed class AwakeTrayTimeItem
    {
        public AwakeTrayTimeItem(string label, uint seconds)
        {
            Label = label;
            Seconds = seconds;
        }

        /// <summary>
        /// Gets the label shown in the tray menu.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the keep-awake duration in seconds.
        /// </summary>
        public uint Seconds { get; }
    }
}

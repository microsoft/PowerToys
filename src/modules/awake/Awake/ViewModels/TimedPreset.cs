// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Awake.ViewModels
{
    /// <summary>
    /// Represents a single preset duration shown in the flyout's Timed sub-section.
    /// </summary>
    public sealed partial class TimedPreset : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public TimedPreset(string label, uint seconds)
        {
            Label = label;
            Seconds = seconds;
        }

        public string Label { get; }

        public uint Seconds { get; }
    }
}

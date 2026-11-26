// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Xaml.Media.Imaging;

using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Properties;

namespace WorkspacesLauncherUI.Models
{
    /// <summary>
    /// Model representing an application's launch status in the Launcher UI.
    /// Drives the display of the spinner (Loading), checkmark/X glyph (StateGlyph),
    /// and color (StateColor) for each app row.
    /// </summary>
    public partial class AppLaunching : ObservableObject
    {
        public bool Loading => LaunchState == LaunchingState.Waiting || LaunchState == LaunchingState.Launched;

        public bool IsSkipped => LaunchState == LaunchingState.Skipped;

        public bool ShowStateGlyph => !Loading && !IsSkipped;

        public string StateDescription => LaunchState switch
        {
            LaunchingState.Waiting => Resources.LaunchStateWaiting,
            LaunchingState.Launched => Resources.LaunchStateLaunched,
            LaunchingState.LaunchedAndMoved => Resources.LaunchStateLaunchedAndMoved,
            LaunchingState.Failed => Resources.LaunchStateFailed,
            LaunchingState.Canceled => Resources.LaunchStateCanceled,
            LaunchingState.Skipped => Resources.LaunchStateSkipped,
            _ => throw new System.InvalidOperationException("Unknown application launch state."),
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressAutomationId))]
        [NotifyPropertyChangedFor(nameof(StateAutomationId))]
        public partial string Name { get; set; }

        public string ProgressAutomationId => $"LaunchProgress_{Name}";

        public string StateAutomationId => $"LaunchState_{Name}";

        public string AppPath { get; set; }

        public BitmapImage IconImage { get; set; }

        public string PackagedName { get; set; }

        public string Aumid { get; set; }

        public string PwaAppId { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Loading))]
        [NotifyPropertyChangedFor(nameof(LaunchStateInt))]
        [NotifyPropertyChangedFor(nameof(IsSkipped))]
        [NotifyPropertyChangedFor(nameof(ShowStateGlyph))]
        [NotifyPropertyChangedFor(nameof(StateDescription))]
        public partial LaunchingState LaunchState { get; set; }

        public int LaunchStateInt => (int)LaunchState;
    }
}

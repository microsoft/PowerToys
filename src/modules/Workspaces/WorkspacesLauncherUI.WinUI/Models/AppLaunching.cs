// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;

using Microsoft.UI.Xaml.Media;

using WorkspacesLauncherUI.Data;

namespace WorkspacesLauncherUI.Models
{
    /// <summary>
    /// Model representing an application's launch status in the Launcher UI.
    /// Drives the display of the spinner (Loading), checkmark/X glyph (StateGlyph),
    /// and color (StateColor) for each app row.
    /// </summary>
    public class AppLaunching : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public bool Loading => LaunchState == LaunchingState.Waiting || LaunchState == LaunchingState.Launched;

        public string Name { get; set; }

        public string AppPath { get; set; }

        public string PackagedName { get; set; }

        public string Aumid { get; set; }

        public string PwaAppId { get; set; }

        public LaunchingState LaunchState { get; set; }

        public string StateGlyph
        {
            get => LaunchState switch
            {
                LaunchingState.LaunchedAndMoved => "\U0000F78C",
                LaunchingState.Failed => "\U0000EF2C",
                _ => "\U0000EF2C",
            };
        }

        public Brush StateColor
        {
            get => LaunchState switch
            {
                LaunchingState.LaunchedAndMoved => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)),
                LaunchingState.Failed => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 254, 0, 0)),
                _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 254, 0, 0)),
            };
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}

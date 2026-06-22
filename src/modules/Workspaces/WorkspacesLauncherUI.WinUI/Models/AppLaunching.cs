// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

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

        public BitmapImage IconImage { get; set; }

        public string PackagedName { get; set; }

        public string Aumid { get; set; }

        public string PwaAppId { get; set; }

        private LaunchingState _launchState;

        public LaunchingState LaunchState
        {
            get => _launchState;
            set
            {
                if (_launchState != value)
                {
                    _launchState = value;
                    _stateColorBrush = null;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(LaunchState)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(Loading)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(StateGlyph)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(StateColor)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(StateColorValue)));
                }
            }
        }

        public string StateGlyph
        {
            get => LaunchState switch
            {
                LaunchingState.LaunchedAndMoved => "\U0000F78C",
                LaunchingState.Failed => "\U0000EF2C",
                _ => "\U0000EF2C",
            };
        }

        private SolidColorBrush _stateColorBrush;

        public Brush StateColor
        {
            get => _stateColorBrush ??= new SolidColorBrush(StateColorValue);
        }

        public Windows.UI.Color StateColorValue
        {
            get => LaunchState switch
            {
                LaunchingState.LaunchedAndMoved => Windows.UI.Color.FromArgb(255, 0, 128, 0),
                LaunchingState.Failed => Windows.UI.Color.FromArgb(255, 254, 0, 0),
                _ => Windows.UI.Color.FromArgb(255, 254, 0, 0),
            };
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}

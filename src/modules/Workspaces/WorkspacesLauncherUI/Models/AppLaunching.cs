// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ManagedCommon;
using WorkspacesCsharpLibrary.Models;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Properties;

namespace WorkspacesLauncherUI.Models
{
    public class AppLaunching : BaseApplication, IDisposable
    {
        public bool Loading => LaunchState == LaunchingState.Waiting || LaunchState == LaunchingState.Launched;

        public bool IsSkipped => LaunchState == LaunchingState.Skipped;

        public bool ShowStateGlyph => !Loading && !IsSkipped;

        public string Name { get; set; }

        public LaunchingState LaunchState { get; set; }

        public string StateDescription => LaunchState switch
        {
            LaunchingState.Waiting => Resources.LaunchStateWaiting,
            LaunchingState.Launched => Resources.LaunchStateLaunched,
            LaunchingState.LaunchedAndMoved => Resources.LaunchStateLaunchedAndMoved,
            LaunchingState.Failed => Resources.LaunchStateFailed,
            LaunchingState.Canceled => Resources.LaunchStateCanceled,
            LaunchingState.Skipped => Resources.LaunchStateSkipped,
            _ => throw new InvalidOperationException("Unknown application launch state."),
        };

        public string StateGlyph
        {
            get => LaunchState switch
            {
                LaunchingState.LaunchedAndMoved => "\U0000F78C",
                LaunchingState.Failed => "\U0000EF2C",
                LaunchingState.Skipped => "\U0000E738",
                _ => "\U0000EF2C",
            };
        }

        public System.Windows.Media.Brush StateColor
        {
            get => LaunchState switch
            {
                LaunchingState.LaunchedAndMoved => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 128, 0)),
                LaunchingState.Failed => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 254, 0, 0)),
                LaunchingState.Skipped => System.Windows.SystemColors.GrayTextBrush,
                _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 254, 0, 0)),
            };
        }
    }
}

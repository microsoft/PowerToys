// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using CommunityToolkit.Mvvm.Messaging;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

using WinRT.Interop;
using WinUIEx;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Messages;

namespace WorkspacesEditor.Views
{
    public sealed partial class SnapshotWindow : WindowEx
    {
        private bool _captured;

        public SnapshotWindow()
        {
            this.InitializeComponent();

            this.Title = ResourceLoaderInstance.ResourceLoader?.GetString("SnapshotWindowTitle") ?? "Snapshot Creator";

            AppWindow.SetIcon("Assets/Workspaces/Workspaces.ico");

            // Custom title bar
            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            SetTitleBar(AppTitleBar);
            AppTitleBar.Title = this.Title;

            // Center the small dialog on screen
            this.CenterOnScreen();

            this.Closed += OnClosed;

            // Set focus to the Capture button when window loads
            this.Activated += (s, e) =>
            {
                var snapshotHwnd = WindowNative.GetWindowHandle(this);
                SetForegroundWindow(snapshotHwnd);
                SnapshotButton.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            };

            // Handle Escape key to cancel
            this.Content.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    this.Close();
                }
            };
        }

        private void SnapshotButtonClicked(object sender, RoutedEventArgs e)
        {
            _captured = true;
            this.Close();
            StrongReferenceMessenger.Default.Send(new SnapshotCapturedMessage());
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            if (!_captured)
            {
                StrongReferenceMessenger.Default.Send(new SnapshotCancelledMessage());
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}

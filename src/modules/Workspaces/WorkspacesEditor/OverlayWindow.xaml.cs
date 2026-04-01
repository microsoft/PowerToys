// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;

using WorkspacesEditor.Utils;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private int _targetX;
        private int _targetY;
        private int _targetWidth;
        private int _targetHeight;

        public OverlayWindow()
        {
            InitializeComponent();
            SourceInitialized += OnWindowSourceInitialized;
        }

        /// <summary>
        /// Sets the target bounds for the overlay window.
        /// The window will be positioned using DPI-unaware context after initialization.
        /// </summary>
        public void SetTargetBounds(int x, int y, int width, int height)
        {
            _targetX = x;
            _targetY = y;
            _targetWidth = width;
            _targetHeight = height;

            // Set initial WPF properties (will be corrected after HWND creation)
            Left = x;
            Top = y;
            Width = width;
            Height = height;
        }

        private void OnWindowSourceInitialized(object sender, EventArgs e)
        {
            // Reposition window using DPI-unaware context to match the virtual coordinates.
            // This fixes overlay positioning on mixed-DPI multi-monitor setups.
            NativeMethods.SetWindowPositionDpiUnaware(this, _targetX, _targetY, _targetWidth, _targetHeight);
        }
    }
}

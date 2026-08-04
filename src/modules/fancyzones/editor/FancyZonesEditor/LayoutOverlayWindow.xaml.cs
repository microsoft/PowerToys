// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using FancyZonesEditor.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;
using WinUIEx;

namespace FancyZonesEditor
{
    /// <summary>
    /// Full-work-area, borderless window that hosts the layout preview or the zone editor for
    /// a single monitor.
    /// </summary>
    public sealed partial class LayoutOverlayWindow : WindowEx
    {
        public LayoutOverlayWindow()
        {
            InitializeComponent();

            Hwnd = WindowNative.GetWindowHandle(this);
            ExtendsContentIntoTitleBar = true;

            // Keep the overlay out of the taskbar and Alt+Tab.
            NativeMethods.SetWindowStyleToolWindow(Hwnd);
        }

        public IntPtr Hwnd { get; }

        /// <summary>
        /// Gets or sets the surface shown on this monitor. Replaces <c>Window.Content</c>,
        /// which WinUI reserves for the window's own root element.
        /// </summary>
        public UIElement OverlayContent
        {
            get => RootGrid.Children.Count > 0 ? RootGrid.Children[0] : null;

            set
            {
                RootGrid.Children.Clear();
                if (value != null)
                {
                    RootGrid.Children.Add(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the data context inherited by <see cref="OverlayContent"/>.
        /// WinUI's Window is not a DependencyObject and therefore has no DataContext.
        /// </summary>
        public object OverlayDataContext
        {
            get => RootGrid.DataContext;
            set => RootGrid.DataContext = value;
        }

        /// <summary>
        /// Gets or sets the opacity of the overlay surface (used by debug mode tinting).
        /// </summary>
        public double OverlayOpacity
        {
            get => RootGrid.Opacity;
            set => RootGrid.Opacity = value;
        }

        /// <summary>
        /// Gets or sets the backdrop brush of the overlay surface (used by debug mode tinting).
        /// </summary>
        public Microsoft.UI.Xaml.Media.Brush OverlayBackground
        {
            get => RootGrid.Background;
            set => RootGrid.Background = value;
        }

        public void AddKeyHandlers(KeyEventHandler keyDown, KeyEventHandler keyUp)
        {
            RootGrid.KeyDown += keyDown;
            RootGrid.KeyUp += keyUp;
        }
    }
}

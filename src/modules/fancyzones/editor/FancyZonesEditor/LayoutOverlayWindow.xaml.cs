// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace FancyZonesEditor
{
    /// <summary>
    /// Full-work-area, borderless window that hosts the layout preview or the zone editor for
    /// a single monitor.
    /// </summary>
    /// <remarks>
    /// Derives from the shared <see cref="TransparentWindow"/> so the WPF
    /// <c>AllowsTransparency="True"</c> scrim survives the migration: the host HWND is made
    /// see-through with a <c>TransparentTintBackdrop</c> and the visible tint comes from the
    /// root grid's semi-transparent <c>BackdropBrush</c>. Unlike a transient toast this overlay
    /// must take keyboard focus for the zone editors, so it is shown with the regular
    /// <c>Activate()</c> rather than the base class's no-activate <c>Show()</c>.
    /// </remarks>
    public sealed partial class LayoutOverlayWindow : TransparentWindow
    {
        public LayoutOverlayWindow()
        {
            InitializeComponent();

            Hwnd = WindowNative.GetWindowHandle(this);

            // The overlay spans a whole monitor, so its edges coincide with the screen edges and
            // any residual DWM frame would read as a full-screen outline.
            ApplyFullBleedHardening();
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

        /// <summary>
        /// Re-applies the transparent, frameless chrome. Windows resets some DWM attributes
        /// (border color, corner preference) when a window crosses a DPI boundary, which would
        /// otherwise reveal an OS-drawn outline around the monitor-sized overlay.
        /// </summary>
        public void ReapplyChrome()
        {
            ApplyTransparentChrome();
            ApplyFullBleedHardening();
        }
    }
}

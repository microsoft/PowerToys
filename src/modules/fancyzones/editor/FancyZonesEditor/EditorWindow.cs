// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

namespace FancyZonesEditor
{
    /// <summary>
    /// Shared base for the canvas and grid layout editor windows.
    /// Derives from <see cref="WindowEx"/> so the fixed-size, non-maximizable chrome can be
    /// declared in XAML the way the WPF <c>Window</c> properties used to be.
    /// </summary>
    public partial class EditorWindow : WindowEx
    {
        public EditorWindow(LayoutModel editingLayout)
        {
            EditingLayout = editingLayout;
            Closed += OnWindowClosed;
        }

        public LayoutModel EditingLayout { get; set; }

        /// <summary>
        /// Makes this window an owned window of the overlay it edits. Replaces WPF's
        /// <c>Window.Owner</c>, which WinUI 3 does not provide.
        /// </summary>
        /// <param name="ownerHwnd">Handle of the layout overlay window.</param>
        public void SetOwner(IntPtr ownerHwnd)
        {
            NativeMethods.SetWindowOwner(WindowNative.GetWindowHandle(this), ownerHwnd);
        }

        /// <summary>
        /// Centers the window on the monitor the layout overlay covers, replacing WPF's
        /// <c>WindowStartupLocation="CenterOwner"</c>. Call after the content has been sized.
        /// </summary>
        protected void CenterOnOverlayMonitor()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);

            // Move onto the overlay's monitor first (virtual coordinates, matching how the
            // overlay itself is positioned), then center within that monitor's work area.
            Rect workArea = App.Overlay.WorkArea;
            NativeMethods.SetWindowPositionDpiUnaware(hwnd, (int)workArea.X, (int)workArea.Y, AppWindow.Size.Width, AppWindow.Size.Height);

            var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            RectInt32 area = display.WorkArea;
            AppWindow.Move(new PointInt32(
                area.X + ((area.Width - AppWindow.Size.Width) / 2),
                area.Y + ((area.Height - AppWindow.Size.Height) / 2)));
        }

        /// <summary>
        /// WinUI 3 has no <c>SizeToContent</c>; this measures the root element and resizes the
        /// client area to match, converting from DIPs to physical pixels.
        /// </summary>
        protected void SizeToContent()
        {
            if (Content is not FrameworkElement root)
            {
                return;
            }

            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size desired = root.DesiredSize;

            double scale = root.XamlRoot?.RasterizationScale ?? 1.0;
            AppWindow.ResizeClient(new SizeInt32(
                (int)Math.Ceiling(desired.Width * scale),
                (int)Math.Ceiling(desired.Height * scale)));
        }

        protected void OnSave(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            // If new custom Canvas layout is created (i.e. edited Blank layout),
            // its type needs to be updated
            if (EditingLayout.Type == LayoutType.Blank)
            {
                EditingLayout.Type = LayoutType.Custom;
            }

            EditingLayout.Persist();

            App.FancyZonesEditorIO.SerializeLayoutTemplates();
            App.FancyZonesEditorIO.SerializeCustomLayouts();

            Close();
        }

        protected void OnCancel(object sender, RoutedEventArgs e)
        {
            // restore backup, clean up
            App.Overlay.EndEditing(EditingLayout);

            // select and draw applied layout
            var settings = ((App)Application.Current).MainWindowSettings;
            settings.SetSelectedModel(settings.AppliedModel);
            App.Overlay.CurrentDataContext = settings.AppliedModel;

            Close();
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            App.Overlay.CloseEditor();
        }
    }
}

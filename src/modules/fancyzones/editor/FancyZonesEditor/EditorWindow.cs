// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using ManagedCommon;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace FancyZonesEditor
{
    /// <summary>
    /// Shared base for the canvas and grid layout editor windows.
    /// Derives from <see cref="OverlayChildWindow"/> so the fixed-size, non-maximizable chrome can
    /// be declared in XAML the way the WPF <c>Window</c> properties used to be, and so the window
    /// is sized to its content and centered on the overlay's monitor.
    /// </summary>
    public partial class EditorWindow : OverlayChildWindow
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
            NativeMethods.SetWindowOwner(Hwnd, ownerHwnd);
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

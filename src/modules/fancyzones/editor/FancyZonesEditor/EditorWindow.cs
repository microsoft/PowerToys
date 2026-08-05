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
            Closed += OnWindowClosing;
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

        /// <summary>
        /// Reuses this tool window for a new editing session and brings it over the active overlay.
        /// </summary>
        /// <param name="editingLayout">Layout being edited.</param>
        /// <param name="ownerHwnd">Handle of the active layout overlay.</param>
        public virtual void PrepareForEditing(LayoutModel editingLayout, IntPtr ownerHwnd)
        {
            EditingLayout = editingLayout;
            SetOwner(ownerHwnd);
            PrePlaceOnOverlayMonitor();
            SizeToContentAndCenter();
            RevealWindow();
            Activate();
        }

        protected void OnSave(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            if (EditingLayout == null)
            {
                return;
            }

            // If new custom Canvas layout is created (i.e. edited Blank layout),
            // its type needs to be updated
            if (EditingLayout.Type == LayoutType.Blank)
            {
                EditingLayout.Type = LayoutType.Custom;
            }

            var settings = ((App)Application.Current).MainWindowSettings;
            if (EditingLayout == settings.AppliedModel)
            {
                App.Overlay.Monitors[App.Overlay.CurrentDesktop].SetLayoutSettings(EditingLayout);
            }

            EditingLayout.Persist();

            App.FancyZonesEditorIO.SerializeAppliedLayouts();
            App.FancyZonesEditorIO.SerializeLayoutTemplates();
            App.FancyZonesEditorIO.SerializeCustomLayouts();
            App.FancyZonesEditorIO.SerializeLayoutHotkeys();
            App.FancyZonesEditorIO.SerializeDefaultLayouts();
            App.Overlay.EndEditing(null);

            FinishEditing();
        }

        protected void OnCancel(object sender, RoutedEventArgs e)
        {
            if (EditingLayout == null)
            {
                return;
            }

            LayoutModel canceledLayout = EditingLayout;
            bool isTransient = !MainWindowSettingsModel.TemplateModels.Contains(canceledLayout) &&
                               !MainWindowSettingsModel.CustomModels.Contains(canceledLayout);

            // restore backup, clean up
            App.Overlay.EndEditing(canceledLayout);

            // select and draw applied layout
            var settings = ((App)Application.Current).MainWindowSettings;
            settings.SetSelectedModel(settings.AppliedModel);
            App.Overlay.CurrentDataContext = settings.AppliedModel;

            FinishEditing();

            if (isTransient)
            {
                canceledLayout.Dispose();
            }
        }

        private void FinishEditing()
        {
            ConcealWindow();
            App.Overlay.CloseEditor();
            OnEditingFinished();
            EditingLayout = null;
        }

        protected virtual void OnEditingFinished()
        {
        }

        private void OnWindowClosing(object sender, WindowEventArgs args)
        {
            if (Application.Current is not App app || app.IsShuttingDown)
            {
                return;
            }

            args.Handled = true;
            OnCancel(sender, null);
        }
    }
}

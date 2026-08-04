// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;
using ManagedCommon;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace FancyZonesEditor
{
    public sealed partial class CanvasEditorWindow : EditorWindow
    {
        public CanvasEditorWindow(CanvasLayoutModel layout)
            : base(layout)
        {
            InitializeComponent();

            // The WPF markup reached the layout through a RelativeSource binding on the
            // Window; WinUI's Window is not a DependencyObject, so the DataContext is set here.
            NewZoneButton.DataContext = layout;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(DragBar);

            RootGrid.KeyUp += CanvasEditorWindow_KeyUp;
            RootGrid.KeyDown += CanvasEditorWindow_KeyDown;
        }

        private static bool IsCtrlKeyDown()
        {
            return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            SizeToContentAndCenter();
            NewZoneButton.Focus(FocusState.Programmatic);
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Add zone");
            if (EditingLayout is CanvasLayoutModel canvas)
            {
                canvas.AddZone();
            }
        }

        private new void OnCancel(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Cancel changes");
            base.OnCancel(sender, e);
        }

        private void CanvasEditorWindow_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                OnCancel(sender, null);
            }
        }

        private void CanvasEditorWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab && IsCtrlKeyDown())
            {
                e.Handled = true;
                App.Overlay.FocusEditor();
            }
        }
    }
}

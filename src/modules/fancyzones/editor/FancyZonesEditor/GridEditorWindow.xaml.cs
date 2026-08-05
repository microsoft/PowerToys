// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FancyZonesEditor
{
    public sealed partial class GridEditorWindow : EditorWindow
    {
        public GridEditorWindow(GridLayoutModel model)
            : base(model)
        {
            InitializeComponent();

            PrePlaceOnOverlayMonitor();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(DragBar);

            RootGrid.KeyUp += GridEditorWindow_KeyUp;
            RootGrid.KeyDown += ((App)Application.Current).App_KeyDown;
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            SizeToContentAndCenter();
        }

        /// <inheritdoc />
        public override void PrepareForEditing(LayoutModel editingLayout, System.IntPtr ownerHwnd)
        {
            base.PrepareForEditing(editingLayout, ownerHwnd);
            SaveButton.Focus(FocusState.Programmatic);
        }

        private void GridEditorWindow_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                OnCancel(sender, null);
            }

            ((App)Application.Current).App_KeyUp(sender, e);
        }
    }
}

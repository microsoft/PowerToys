// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using ColorPicker.Helpers;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for ColorEditorWindow.xaml
    /// </summary>
    public partial class ColorEditorWindow : Window
    {
        private readonly AppStateHandler _appStateHandler;

        public ColorEditorWindow(AppStateHandler appStateHandler)
        {
            InitializeComponent();
            _appStateHandler = appStateHandler;
            Closing += ColorEditorWindow_Closing;
        }

        private void ColorEditorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            _appStateHandler.EndUserSession();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.SetToolWindowStyle(this);
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TopToolbar.Logging;

namespace TopToolbar
{
    public partial class App : Application, IDisposable
    {
        private ToolbarWindow _window;

        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new ToolbarWindow();
            _window.Activate();
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                AppLogger.LogError("Unhandled UI exception", e.Exception);
            }
            catch
            {
            }

            e.Handled = true;
        }

        public void Dispose()
        {
            _window?.Dispose();
            _window = null;
            GC.SuppressFinalize(this);
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace TopToolbar
{
    public partial class App : Application, IDisposable
    {
        private ToolbarWindow _window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new ToolbarWindow();
            _window.Activate();
        }

        public void Dispose()
        {
            _window?.Dispose();
            _window = null;
            GC.SuppressFinalize(this);
        }
    }
}

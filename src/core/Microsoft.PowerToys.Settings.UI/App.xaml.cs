// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI;
using Windows.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Hide the Xaml Island window
            var coreWindow = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            var coreWindowInterop = Interop.GetInterop(coreWindow);
            Interop.ShowWindow(coreWindowInterop.WindowHandle, Interop.SW_HIDE);
        }
    }
}

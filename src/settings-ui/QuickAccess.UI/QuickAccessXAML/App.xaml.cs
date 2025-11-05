// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.QuickAccess;

public partial class App : Application
{
    private MainWindow? _window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var launchContext = QuickAccessLaunchContext.Parse(Environment.GetCommandLineArgs());
        _window = new MainWindow(launchContext);
        _window.Activate();
    }
}

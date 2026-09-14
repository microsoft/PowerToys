// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Windows;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var paths = e.Args;
        string? error = null;
        if (e.Args is [SelectionPayload.InputSwitch])
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true));
                paths = await SelectionPayload.ReadAsync(reader, timeout.Token).WaitAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                paths = [];
                error = exception is OperationCanceledException ? "The Explorer selection did not arrive in time. Try again." : "Could not receive the Explorer selection: " + exception.Message;
            }
        }

        MainWindow = new MainWindow(paths, error);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow.Show();
    }
}

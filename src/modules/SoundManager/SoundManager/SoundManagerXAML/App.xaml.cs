// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT.Interop;

namespace SoundManager
{
    public partial class App : Application, IDisposable
    {
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        private MainWindow _window;
        private string _pipeName = string.Empty;

        public App()
        {
            InitializeComponent();

            // A crash that gets out of the window would end the app without a word in the log, so it is written down before the app goes.
            UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            Logger.LogError($"Sound Manager ran into a problem it did not expect: {args.Message}");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // The module interface starts the window with the process id of the runner and the name of the pipe it listens on.
            var commandLine = Environment.GetCommandLineArgs();
            var hasArguments = commandLine.Length > 2;

            _pipeName = hasArguments ? commandLine[2] : string.Empty;

            if (hasArguments && int.TryParse(commandLine[1], CultureInfo.InvariantCulture, out int runnerPid))
            {
                RunnerHelper.WaitForPowerToysRunner(runnerPid, () =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        Dispose();
                        Environment.Exit(0);
                    });
                });
            }

            ShowWindow();
        }

        // Brings the window back. A second copy of the app hands its activation over to this one, and that is what makes the window come back.
        public void ActivateWindow()
        {
            _dispatcherQueue.TryEnqueue(ShowWindow);
        }

        private void ShowWindow()
        {
            if (_window == null)
            {
                _window = new MainWindow(_pipeName);
                _window.Closed += Window_Closed;
            }

            // A window that was made smaller is not brought back by showing it.
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter && IsMinimized(_window))
            {
                presenter.Restore();
            }

            _window.AppWindow.Show();
            _window.Activate();
        }

        // Tells whether the window has been made smaller. The windowing API of WinUI does not say that, so Windows itself is asked.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr windowHandle);

        private static bool IsMinimized(Window window)
        {
            return IsIconic(WindowNative.GetWindowHandle(window));
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _window = null;
        }

        public void Dispose()
        {
            _window?.Dispose();
            _window = null;
            GC.SuppressFinalize(this);
        }
    }
}

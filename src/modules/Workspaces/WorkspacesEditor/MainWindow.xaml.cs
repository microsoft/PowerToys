// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

using ManagedCommon;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        public MainViewModel MainViewModel { get; set; }

        private CancellationTokenSource cancellationToken = new CancellationTokenSource();

        private static MainPage _mainPage;

        public MainWindow(MainViewModel mainViewModel)
        {
            MainViewModel = mainViewModel;
            mainViewModel.SetMainWindow(this);

            WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this);
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(windowInteropHelper.Handle);
            double dpi = MonitorHelper.GetScreenDpiFromScreen(screen);
            this.Height = screen.WorkingArea.Height / dpi * 0.90;
            this.Width = screen.WorkingArea.Width / dpi * 0.75;
            this.Top = screen.WorkingArea.Top + (int)(screen.WorkingArea.Height / dpi * 0.05);
            this.Left = screen.WorkingArea.Left + (int)(screen.WorkingArea.Width / dpi * 0.125);

            InitializeComponent();

            _mainPage = new MainPage(mainViewModel);

            ContentFrame.Navigate(_mainPage);

            MaxWidth = SystemParameters.PrimaryScreenWidth;
            MaxHeight = SystemParameters.PrimaryScreenHeight;

            Common.UI.NativeEventWaiter.WaitForEventLoop(
                PowerToys.Interop.Constants.WorkspacesHotkeyEvent(),
                () =>
                {
                    if (ApplicationIsInFocus())
                    {
                        Environment.Exit(0);
                    }
                    else
                    {
                        if (WindowState == WindowState.Minimized)
                        {
                            WindowState = WindowState.Normal;
                        }

                        // Get the window handle of the Workspaces Editor window
                        IntPtr handle = new WindowInteropHelper(this).Handle;
                        WindowHelpers.BringToForeground(handle);

                        InvalidateVisual();
                    }
                },
                Application.Current.Dispatcher,
                cancellationToken.Token);
        }

        private void OnClosing(object sender, EventArgs e)
        {
            cancellationToken.Dispose();
            App.Current.Shutdown();
        }

        // This is required to fix a WPF rendering bug when using custom chrome
        private void OnContentRendered(object sender, EventArgs e)
        {
            // Get the window handle of the Workspaces Editor window
            IntPtr handle = new WindowInteropHelper(this).Handle;
            WindowHelpers.BringToForeground(handle);

            InvalidateVisual();
        }

        public void ShowPage(ProjectEditor editPage)
        {
            ContentFrame.Navigate(editPage);
        }

        public void SwitchToMainView()
        {
            ContentFrame.GoBack();
        }

        public static bool ApplicationIsInFocus()
        {
            var activatedHandle = NativeMethods.GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Environment.ProcessId;
            int activeProcId;
            _ = NativeMethods.GetWindowThreadProcessId(activatedHandle, out activeProcId);

            return activeProcId == procId;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}

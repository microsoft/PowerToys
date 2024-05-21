// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Interop;
using ProjectsEditor.Utils;
using ProjectsEditor.ViewModels;

namespace ProjectsEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool haveTriedToGetFocusAlready;

        public MainViewModel MainViewModel { get; set; }

        private static MainPage _mainPage;

        public MainWindow(MainViewModel mainViewModel)
        {
            MainViewModel = mainViewModel;
            mainViewModel.SetMainWindow(this);
            InitializeComponent();

            _mainPage = new MainPage(mainViewModel);

            ContentFrame.Navigate(_mainPage);

            MaxWidth = SystemParameters.PrimaryScreenWidth;
            MaxHeight = SystemParameters.PrimaryScreenHeight;
        }

        private void BringToFront()
        {
            // Get the window handle of the Projects Editor window
            IntPtr handle = new WindowInteropHelper(this).Handle;

            // Get the handle of the window currently in the foreground
            IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();

            // Get the thread IDs of the current thread and the thread of the foreground window
            uint currentThreadId = NativeMethods.GetCurrentThreadId();
            uint activeThreadId = NativeMethods.GetWindowThreadProcessId(foregroundWindowHandle, IntPtr.Zero);

            // Check if the active thread is different from the current thread
            if (activeThreadId != currentThreadId)
            {
                // Attach the input processing mechanism of the current thread to the active thread
                NativeMethods.AttachThreadInput(activeThreadId, currentThreadId, true);

                // Set the Projects Editor window as the foreground window
                NativeMethods.SetForegroundWindow(handle);

                // Detach the input processing mechanism of the current thread from the active thread
                NativeMethods.AttachThreadInput(activeThreadId, currentThreadId, false);
            }
            else
            {
                // Set the Projects Editor window as the foreground window
                NativeMethods.SetForegroundWindow(handle);
            }

            // Bring the Projects Editor window to the foreground and activate it
            NativeMethods.SwitchToThisWindow(handle, true);

            haveTriedToGetFocusAlready = true;
        }

        private void OnClosing(object sender, EventArgs e)
        {
            App.Current.Shutdown();
        }

        // This is required to fix a WPF rendering bug when using custom chrome
        private void OnContentRendered(object sender, EventArgs e)
        {
            if (!haveTriedToGetFocusAlready)
            {
                BringToFront();
            }

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
    }
}

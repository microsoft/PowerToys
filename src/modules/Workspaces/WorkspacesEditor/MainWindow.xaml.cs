// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    public partial class MainWindow : Window
    {
        public MainViewModel MainViewModel { get; set; }

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
        }

        private void OnClosing(object sender, EventArgs e)
        {
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
    }
}

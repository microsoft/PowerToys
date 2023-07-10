// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace FileLocksmithUI
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindow(bool isElevated)
        {
            InitializeComponent();
            mainPage.ViewModel.IsElevated = isElevated;
            SetTitleBar();
            Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppTitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                AppTitleTextBlock.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
        }

        private void SetTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
        }

        public void Dispose()
        {
        }
    }
}

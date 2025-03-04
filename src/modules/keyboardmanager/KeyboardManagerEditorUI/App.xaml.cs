﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KeyboardManagerEditorUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            Task.Run(() =>
            {
                Logger.InitializeLogger("\\Keyboard Manager\\WinUI3Editor\\Logs");
                Logger.LogInfo("keyboard-manager WinUI3 editor logger is initialized");
            });

            UnhandledException += App_UnhandledException;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            window = new MainWindow();

            var appWindow = window.AppWindow;
            var titleBar = appWindow.TitleBar;

            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.BackgroundColor = Colors.Transparent;

            var windowSize = new Windows.Graphics.SizeInt32(960, 600);
            appWindow.Resize(windowSize);

            Task.Run(() =>
            {
                App.Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("ms-appx:///Styles/CommonStyle.xaml"),
                });
            }).ContinueWith(_ =>
            {
                window.DispatcherQueue.TryEnqueue(() =>
                {
                    window.Activate();
                    window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        (window.Content as FrameworkElement)?.UpdateLayout();
                    });
                });
            });

            Logger.LogInfo("keyboard-manager WinUI3 editor window is launched");
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        private Window? window;
    }
}

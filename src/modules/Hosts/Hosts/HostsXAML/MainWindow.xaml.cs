// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Hosts.Helpers;
using HostsEditor.Telemetry;
using HostsUILib.Helpers;
using HostsUILib.Views;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using WinUIEx;

namespace Hosts
{
    public sealed partial class MainWindow : WindowEx
    {
        private HostsMainPage MainPage { get; }

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.SetIcon("Assets/Hosts/Hosts.ico");

            var loader = new ResourceLoader("PowerToys.HostsUILib.pri", "PowerToys.HostsUILib/Resources");

            var title = Host.GetService<IElevationHelper>().IsElevated ? loader.GetString("WindowAdminTitle") : loader.GetString("WindowTitle");
            Title = title;
            titleBar.Title = title;

            var handle = this.GetWindowHandle();

            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(handle);
            WindowHelpers.BringToForeground(handle);

            MainPage = Host.GetService<HostsMainPage>();

            PowerToysTelemetry.Log.WriteEvent(new HostEditorStartFinishEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            MainGrid.Children.Add(MainPage);
            Grid.SetRow(MainPage, 1);
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args)
        {
            (Application.Current as App).EtwTrace?.Dispose();
        }
    }
}

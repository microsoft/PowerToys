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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Hosts
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
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
            AppTitleTextBlock.Text = title;

            var handle = this.GetWindowHandle();

            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(handle);
            WindowHelpers.BringToForeground(handle);
            Activated += MainWindow_Activated;

            MainPage = Host.GetService<HostsMainPage>();

            PowerToysTelemetry.Log.WriteEvent(new HostEditorStartFinishEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppTitleTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                AppTitleTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
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

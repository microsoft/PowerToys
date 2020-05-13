using Microsoft.QueryStringDotNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System;

using PowerToys_Settings_Sandbox.ViewModels;

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Toolkit.Uwp.UI.Extensions;

namespace PowerToys_Settings_Sandbox.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();
        
        public MainPage()
        {
            InitializeComponent();
            powerOnLaunchDialog();
        }

        private async void powerOnLaunchDialog()
        {
            Image img = new Image();
            Image sec = new Image();
            Image im = new Image();
            Image se = new Image();
            Image logo = new Image();
            BitmapImage bitmapImage = new BitmapImage();
            img.Width = bitmapImage.DecodePixelWidth = 70;
            img.Height = bitmapImage.DecodePixelHeight = 80;
            bitmapImage.UriSource = new Uri("ms-appx:///Assets/demo.gif");
            img.Source = bitmapImage;

            sec.Width = bitmapImage.DecodePixelWidth = 70;
            sec.Height = bitmapImage.DecodePixelHeight = 80;
            sec.Source = new BitmapImage(new Uri("ms-appx:///Assets/FZTutorial.jpg"));
            se.Width = bitmapImage.DecodePixelWidth = 70;
            im.Height = bitmapImage.DecodePixelHeight = 80;
            se.Source = new BitmapImage(new Uri("ms-appx:///Assets/resizeSettings.gif"));
            im.Width = bitmapImage.DecodePixelWidth = 70;
            im.Source = new BitmapImage(new Uri("ms-appx:///Assets/resizeSettings.gif"));
            logo.Width = bitmapImage.DecodePixelWidth = 70;
            logo.Source = new BitmapImage(new Uri("ms-appx:///Assets/PowerToysAppList.targetsize-20.png"));
            StackPanel masterPanel = new StackPanel();


            StackPanel titlePanel = new StackPanel();
            TextBlock titleText = new TextBlock();
            titleText.Text = "Do more with PowerToys";
            titleText.HorizontalAlignment = HorizontalAlignment.Center;
            titlePanel.Children.Add(logo);
            titlePanel.Children.Add(titleText);

            StackPanel mainPanel = new StackPanel();
            StackPanel previewPanel = new StackPanel();
            StackPanel fzPanel = new StackPanel();
            StackPanel powerLauncherPanel = new StackPanel();
            StackPanel powerNamePanel = new StackPanel();
            TextBlock previewText = new TextBlock();
            TextBlock fzText = new TextBlock();
            TextBlock powerLauncherText = new TextBlock();
            TextBlock poweNameText = new TextBlock();


            previewPanel.Children.Add(img);
            previewText.Text = "preview";
            previewText.FontSize = 10;
            previewPanel.Children.Add(previewText);
            fzPanel.Children.Add(sec);
            fzText.Text = "Fancy Zones";
            fzText.FontSize = 10;
            fzPanel.Children.Add(fzText);

            powerLauncherPanel.Children.Add(im);
            powerLauncherText.Text = " image resizer";
            powerLauncherText.FontSize = 10;
            powerLauncherPanel.Children.Add(powerLauncherText);

            powerNamePanel.Children.Add(se);
            poweNameText.Text = "power name";
            poweNameText.FontSize = 10;
            powerNamePanel.Children.Add(poweNameText);

            mainPanel.Children.Add(previewPanel);
            mainPanel.Children.Add(fzPanel);
            mainPanel.Children.Add(powerLauncherPanel);
            mainPanel.Children.Add(powerNamePanel);
            mainPanel.Orientation = Orientation.Horizontal;
            masterPanel.Children.Add(titlePanel);
            masterPanel.Children.Add(mainPanel);

            ContentDialog onLaunchDialog = new ContentDialog()
            {

                Title = "Welcome to PowerToys",
                Content = masterPanel,
                CloseButtonText = "Not now",
                PrimaryButtonText = "Go to tour",
                DefaultButton = ContentDialogButton.Primary

            };

            ContentDialogResult selection = await onLaunchDialog.ShowAsync();

            if(selection == ContentDialogResult.Primary)
            {
                OpenFirstGeneralSettingsTip();
            }
            else
            {
                //create a toast for later
            }
        }

        private async void powerUpdateDialog()
        {
            Image img = new Image();
            BitmapImage bitmapImage = new BitmapImage();
            img.Width = bitmapImage.DecodePixelWidth = 140;
            img.Height = bitmapImage.DecodePixelHeight = 110;
            bitmapImage.UriSource = new Uri("ms-appx:///Assets/resizeSettings.gif");
            img.Source = bitmapImage;
            StackPanel contentPanel = new StackPanel();
            contentPanel.Children.Add(img);
            ContentDialog updateDialog = new ContentDialog()
            {
                Title = "what's new in PowerToys",
                Content = contentPanel,
                PrimaryButtonText = "Got it",
                DefaultButton = ContentDialogButton.Primary

            };

            await updateDialog.ShowAsync();

        }

        // This method opens the first teaching tip on the General Settings page
        // Should open automatically only on initial install after user starts tutorial
        private void OpenFirstGeneralSettingsTip()
        {
            GeneralSettingsTip.IsOpen = true;
        }

        // This method opens the second teaching tip
        private void OpenRunAsUserTip()
        {
            GeneralSettingsTip.IsOpen = false;
            RunAsUserTip.IsOpen = true;
        }

        // This method opens the last teaching tip
        private void OpenFinalGeneralSettingsTip()
        {
            RunAsUserTip.IsOpen = false;
            FinalGeneralSettingsTip.IsOpen = true;
        }

        // This method closes all teaching tips
        private void CloseTeachingTips()
        {
            GeneralSettingsTip.IsOpen = false;
            RunAsUserTip.IsOpen = false;
            FinalGeneralSettingsTip.IsOpen = false;
        }
    }
}

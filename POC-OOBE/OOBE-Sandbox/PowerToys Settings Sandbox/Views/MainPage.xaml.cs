
using System;
using Windows.UI.Xaml.Controls;
using PowerToys_Settings_Sandbox.ViewModels;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.Helpers;
using Windows.UI.Notifications;
using Windows.Storage;

namespace PowerToys_Settings_Sandbox.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();
        
        public MainPage()
        {
            InitializeComponent();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string status)
            {
                /// <summary>
                /// Will run appropriate startups when toast is clicked
                /// </summary>
                if (status == "FirstOpen")
                {
                    PowerOnLaunchDialog();
                }
                else if (status == "NewUpdateOpen")
                {
                    DisplayUpdateDialog();
                }
                /// <summary>
                /// Check for current status of app (new update or new install) on launch
                /// Comment out this section if using sandbox notifications in App.xaml.cs
                /// Replace the SystemInformation with flags present in current powertoys app if required
                /// </summary>
                else
                {
                    if (SystemInformation.IsFirstRun)
                    {
                        PowerOnLaunchDialog();
                    }
                    else if (SystemInformation.IsAppUpdated)
                    {
                        DisplayUpdateDialog();
                    }
                }
            }
        }

        public async void PowerOnLaunchDialog()
        {
            onLaunchContentDialog dialog = new onLaunchContentDialog();
            dialog.PrimaryButtonClick += Dialog_PrimaryButtonClick;
            await dialog.ShowAsync();
        }

        private async void DisplayUpdateDialog()
        {
            ContentDialog updateDialog = new UpdateContentDialog();
            await updateDialog.ShowAsync();
        }
        
        private void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            OpenRunAsUserTip();
        }

        private void OpenRunAsUserTip()
        {
            RunAsUserTip.IsOpen = true;
        }

        private void OpenFinalGeneralSettingsTip()
        {
            RunAsUserTip.IsOpen = false;
            FinalGeneralSettingsTip.IsOpen = true;
        }
    }
}

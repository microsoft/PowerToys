
using System;
using Windows.UI.Xaml.Controls;
using PowerToys_Settings_Sandbox.ViewModels;


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
            OpenFirstGeneralSettingsTip();
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

using PowerToys_Settings_Sandbox.ViewModels;
using System;
using System.Collections.Generic;

using Windows.UI.Xaml.Controls;


// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PowerToys_Settings_Sandbox.Views
{
    public sealed partial class onLaunchContentDialog : ContentDialog
    {
        public onLaunchContentDialog()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}

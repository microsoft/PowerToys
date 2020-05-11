using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PowerToys_Settings_Sandbox.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FancyZonesPage : Page
    {
        private bool hideTips = false;
        public FancyZonesPage()
        {
            this.InitializeComponent();

            // if (!HaveExplainedAutoSave())
            // {
                   EnableFeatureTip.IsOpen = true;
                    //AboutFeatureTip.IsOpen = true;
            //     SetHaveExplainedAutoSave();
            //}
        }

        private void ShowAboutFeatureTip()
        {
            if(!hideTips)
            {
                EnableFeatureTip.IsOpen = false;
                AboutFeatureTip.IsOpen = true;
            }
        }

        private void ShowHelpGuideTip()
        {
            
        }

        //maybe can only do one function which is to make all teaching tips false - then each binding would make the other one true

        private void SkipTeachingTips()
        {
            // refactor this later - probably don't need to close all here, can do an else in each command and close the tip ther
            EnableFeatureTip.IsOpen = false;
            AboutFeatureTip.IsOpen = false;
            hideTips = true;
        }
    }
}

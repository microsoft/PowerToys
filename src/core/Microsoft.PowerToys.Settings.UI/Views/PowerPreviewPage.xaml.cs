using Microsoft.PowerToys.Settings.UI.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ServiceModel.Channels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PowerPreviewPage : Page
    {
        private const string POWERTOY_NAME = "File Explorer Preview";

        public PowerPreviewPage()
        {
            this.InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PowerPreviewSettings settings;
            try
            {
                base.OnNavigatedTo(e);
                settings = SettingsUtils.GetSettings<PowerPreviewSettings>(POWERTOY_NAME);
                ToggleSwitch_Preview_SVG.IsOn = settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value;
                ToggleSwitch_Preview_MD.IsOn = settings.properties.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID.value;
            }
            catch(Exception exp)
            {
                settings = new PowerPreviewSettings(POWERTOY_NAME);
                SettingsUtils.SaveSettings(settings.ToJsonString(), POWERTOY_NAME);
                ToggleSwitch_Preview_SVG.IsOn = settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value;
                ToggleSwitch_Preview_MD.IsOn = settings.properties.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID.value;
            }
        }

        private void ToggleSwitch_Preview_SVG_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerPreviewSettings settings = SettingsUtils.GetSettings<PowerPreviewSettings>(POWERTOY_NAME);
                settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value = swt.IsOn;

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }

        private void ToggleSwitch_Preview_MD_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerPreviewSettings settings = SettingsUtils.GetSettings<PowerPreviewSettings>(POWERTOY_NAME);
                settings.properties.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID.value = swt.IsOn;

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }

    }
}

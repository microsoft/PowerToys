// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseJump.Common.Helpers;
using MouseJumpUI.Common.Models.Settings;

namespace Microsoft.PowerToys.Settings.UI.Panels
{
    public sealed partial class MouseJumpPanel : UserControl
    {
        internal MouseUtilsViewModel ViewModel { get; set; }

        public MouseJumpPanel()
        {
            InitializeComponent();
        }

        private void PreviewTypeSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // hide or display controls based on whether the "Custom" preview type is selected
            var selectedPreviewType = this.GetSelectedPreviewType();
            var customPreviewTypeSelected = selectedPreviewType == PreviewType.Custom;
            this.CopyStyleToCustom.IsEnabled = !customPreviewTypeSelected;
            var customControlVisibility = customPreviewTypeSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
            this.MouseUtils_MouseJump_BackgroundColor1.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_BackgroundColor2.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_BorderThickness.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_BorderColor.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_Border3dDepth.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_BorderPadding.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_BezelThickness.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_BezelColor.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_Bezel3dDepth.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_ScreenMargin.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_ScreenColor1.Visibility = customControlVisibility;
            this.MouseUtils_MouseJump_ScreenColor2.Visibility = customControlVisibility;
        }

        private /* async */ void CopyStyleToCustom_Click(object sender, RoutedEventArgs e)
        {
            /*
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var messageBox = this.MouseUtils_MouseJump_CopyToCustomStyle_MessageBox;
            messageBox.Title = resourceLoader.GetString("MouseUtils_MouseJump_CopyToCustomStyle_MessageBox_Title");
            messageBox.PrimaryButtonText = resourceLoader.GetString("MouseUtils_MouseJump_CopyToCustomStyle_MessageBox_PrimaryButtonText");
            messageBox.PrimaryButtonCommand = new RelayCommand(this.MouseUtils_MouseJump_CopyToCustomStyle_MessageBox_PrimaryButtonCommand);
            // await messageBox.ShowAsync();
            this.MouseUtils_MouseJump_CopyToCustomStyle_MessageBox_PrimaryButtonCommand();
            */
        }

        private void MouseUtils_MouseJump_CopyToCustomStyle_MessageBox_PrimaryButtonCommand()
        {
            var selectedPreviewType = this.GetSelectedPreviewType();
            var selectedPreviewStyle = selectedPreviewType switch
            {
                PreviewType.Compact => StyleHelper.CompactPreviewStyle,
                PreviewType.Bezelled => StyleHelper.BezelledPreviewStyle,
                PreviewType.Custom => StyleHelper.BezelledPreviewStyle,
                _ => throw new InvalidOperationException(),
            };

            // convert the color into a string.
            // note that we have to replace Named and System colors with their ARGB equivalents
            // so that serialization returns an ARGB string rather than the Named or System color *name*.
            this.ViewModel.MouseJumpPreviewType = selectedPreviewType.ToString();
            this.ViewModel.MouseJumpBackgroundColor1 = ConfigHelper.SerializeToConfigColorString(
                ConfigHelper.ToUnnamedColor(selectedPreviewStyle.CanvasStyle.BackgroundStyle.Color1));
            this.ViewModel.MouseJumpBackgroundColor2 = ConfigHelper.SerializeToConfigColorString(
                ConfigHelper.ToUnnamedColor(selectedPreviewStyle.CanvasStyle.BackgroundStyle.Color2));
            this.ViewModel.MouseJumpBorderThickness = (int)selectedPreviewStyle.CanvasStyle.BorderStyle.Top;
            this.ViewModel.MouseJumpBorderColor = ConfigHelper.SerializeToConfigColorString(
                ConfigHelper.ToUnnamedColor(selectedPreviewStyle.CanvasStyle.BorderStyle.Color));
            this.ViewModel.MouseJumpBorder3dDepth = (int)selectedPreviewStyle.CanvasStyle.BorderStyle.Depth;
            this.ViewModel.MouseJumpBorderPadding = (int)selectedPreviewStyle.CanvasStyle.PaddingStyle.Top;
            this.ViewModel.MouseJumpBezelThickness = (int)selectedPreviewStyle.ScreenStyle.BorderStyle.Top;
            this.ViewModel.MouseJumpBezelColor = ConfigHelper.SerializeToConfigColorString(
                ConfigHelper.ToUnnamedColor(selectedPreviewStyle.ScreenStyle.BorderStyle.Color));
            this.ViewModel.MouseJumpBezel3dDepth = (int)selectedPreviewStyle.ScreenStyle.BorderStyle.Depth;
            this.ViewModel.MouseJumpScreenMargin = (int)selectedPreviewStyle.ScreenStyle.MarginStyle.Top;
            this.ViewModel.MouseJumpScreenColor1 = ConfigHelper.SerializeToConfigColorString(
                ConfigHelper.ToUnnamedColor(selectedPreviewStyle.ScreenStyle.BackgroundStyle.Color1));
            this.ViewModel.MouseJumpScreenColor2 = ConfigHelper.SerializeToConfigColorString(
                ConfigHelper.ToUnnamedColor(selectedPreviewStyle.ScreenStyle.BackgroundStyle.Color2));
        }

        private PreviewType GetSelectedPreviewType()
        {
            // this needs to match the order of the SegmentedItems in the "Preview Type" Segmented control
            var previewTypeOrder = new PreviewType[]
            {
                PreviewType.Compact, PreviewType.Bezelled, PreviewType.Custom,
            };

            var selectedIndex = this.PreviewTypeSetting.SelectedIndex;
            if ((selectedIndex < 0) || (selectedIndex >= previewTypeOrder.Length))
            {
                throw new InvalidOperationException();
            }

            return previewTypeOrder[selectedIndex];
        }
    }
}

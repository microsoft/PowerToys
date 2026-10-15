// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseJump.Common.Helpers;
using MouseJump.Models.Settings;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class MouseJumpPanel : UserControl
    {
        private static readonly HashSet<string> StylePropertyNames = new()
        {
            nameof(MouseUtilsViewModel.MouseJumpBackgroundColor1),
            nameof(MouseUtilsViewModel.MouseJumpBackgroundColor2),
            nameof(MouseUtilsViewModel.MouseJumpBorderThickness),
            nameof(MouseUtilsViewModel.MouseJumpBorderColor),
            nameof(MouseUtilsViewModel.MouseJumpBorder3dDepth),
            nameof(MouseUtilsViewModel.MouseJumpBorderPadding),
            nameof(MouseUtilsViewModel.MouseJumpBezelThickness),
            nameof(MouseUtilsViewModel.MouseJumpBezelColor),
            nameof(MouseUtilsViewModel.MouseJumpBezel3dDepth),
            nameof(MouseUtilsViewModel.MouseJumpScreenMargin),
            nameof(MouseUtilsViewModel.MouseJumpScreenColor1),
            nameof(MouseUtilsViewModel.MouseJumpScreenColor2),
        };

        private bool isApplyingPreset;

        internal MouseUtilsViewModel ViewModel { get; set; }

        public MouseJumpPanel()
        {
            InitializeComponent();
            Loaded += MouseJumpPanel_Loaded;
            Unloaded += MouseJumpPanel_Unloaded;
        }

        private void PreviewImage_Loaded(object sender, RoutedEventArgs e)
        {
            bool TryFindFrameworkElement(SettingsCard settingsCard, string partName, out FrameworkElement result)
            {
                result = settingsCard.FindDescendants()
                    .OfType<FrameworkElement>()
                    .FirstOrDefault(
                        x => x.Name == partName);
                return result is not null;
            }

            /*
                apply a variation of the "Left" VisualState for SettingsCards
                to center the preview image in the true center of the card
                see https://github.com/CommunityToolkit/Windows/blob/9c7642ff35eaaa51a404f9bcd04b10c7cf851921/components/SettingsControls/src/SettingsCard/SettingsCard.xaml#L334-L347
            */

            var settingsCard = (SettingsCard)sender;

            var partNames = new List<string>
            {
                "PART_HeaderIconPresenterHolder",
                "PART_DescriptionPresenter",
                "PART_HeaderPresenter",
                "PART_ActionIconPresenter",
            };
            foreach (var partName in partNames)
            {
                if (!TryFindFrameworkElement(settingsCard, partName, out var element))
                {
                    continue;
                }

                element.Visibility = Visibility.Collapsed;
            }

            if (TryFindFrameworkElement(settingsCard, "PART_ContentPresenter", out var content))
            {
                Grid.SetRow(content, 1);
                Grid.SetColumn(content, 1);
                content.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }

        private void PreviewTypeSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChanged can fire transiently with SelectedIndex == -1 (e.g. while items are
            // being initialized before the x:Bind two-way binding restores the persisted value).
            // Ignore those intermediate states instead of throwing.
            if (this.PreviewTypeSetting.SelectedIndex < 0)
            {
                return;
            }

            // show the selected preset's values in the style settings so that editing any of them
            // starts from the preset (and switches the style to "Custom", see ViewModel_PropertyChanged)
            var selectedPreviewType = this.GetSelectedPreviewType();
            if (selectedPreviewType != PreviewType.Custom)
            {
                this.ApplyPresetValues(selectedPreviewType);
            }
        }

        private void MouseJumpPanel_Loaded(object sender, RoutedEventArgs e)
        {
            this.ViewModel.PropertyChanged -= this.ViewModel_PropertyChanged;
            this.ViewModel.PropertyChanged += this.ViewModel_PropertyChanged;
        }

        private void MouseJumpPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            this.ViewModel.PropertyChanged -= this.ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // editing any style setting while a preset is selected turns it into a custom style
            if (this.isApplyingPreset || !StylePropertyNames.Contains(e.PropertyName))
            {
                return;
            }

            if (this.ViewModel.MouseJumpPreviewType != nameof(PreviewType.Custom))
            {
                this.ViewModel.MouseJumpPreviewType = nameof(PreviewType.Custom);
            }
        }

        private void ApplyPresetValues(PreviewType presetType)
        {
            var selectedPreviewStyle = presetType switch
            {
                PreviewType.Compact => StyleHelper.CompactPreviewStyle,
                _ => StyleHelper.BezelledPreviewStyle,
            };

            this.isApplyingPreset = true;
            try
            {
                // convert the color into a string.
                // note that we have to replace Named and System colors with their ARGB equivalents
                // so that serialization returns an ARGB string rather than the Named or System color *name*.
                this.ViewModel.MouseJumpBackgroundColor1 = ColorHelper.SerializeToConfigColorString(
                    ColorHelper.ToUnnamedColor(selectedPreviewStyle.CanvasStyle.BackgroundStyle.Color1));
                this.ViewModel.MouseJumpBackgroundColor2 = ColorHelper.SerializeToConfigColorString(
                    ColorHelper.ToUnnamedColor(selectedPreviewStyle.CanvasStyle.BackgroundStyle.Color2));
                this.ViewModel.MouseJumpBorderThickness = (int)selectedPreviewStyle.CanvasStyle.BorderStyle.Top;
                this.ViewModel.MouseJumpBorderColor = ColorHelper.SerializeToConfigColorString(
                    ColorHelper.ToUnnamedColor(selectedPreviewStyle.CanvasStyle.BorderStyle.Color));
                this.ViewModel.MouseJumpBorder3dDepth = (int)selectedPreviewStyle.CanvasStyle.BorderStyle.Depth;
                this.ViewModel.MouseJumpBorderPadding = (int)selectedPreviewStyle.CanvasStyle.PaddingStyle.Top;
                this.ViewModel.MouseJumpBezelThickness = (int)selectedPreviewStyle.ScreenStyle.BorderStyle.Top;
                this.ViewModel.MouseJumpBezelColor = ColorHelper.SerializeToConfigColorString(
                    ColorHelper.ToUnnamedColor(selectedPreviewStyle.ScreenStyle.BorderStyle.Color));
                this.ViewModel.MouseJumpBezel3dDepth = (int)selectedPreviewStyle.ScreenStyle.BorderStyle.Depth;
                this.ViewModel.MouseJumpScreenMargin = (int)selectedPreviewStyle.ScreenStyle.MarginStyle.Top;
                this.ViewModel.MouseJumpScreenColor1 = ColorHelper.SerializeToConfigColorString(
                    ColorHelper.ToUnnamedColor(selectedPreviewStyle.ScreenStyle.BackgroundStyle.Color1));
                this.ViewModel.MouseJumpScreenColor2 = ColorHelper.SerializeToConfigColorString(
                    ColorHelper.ToUnnamedColor(selectedPreviewStyle.ScreenStyle.BackgroundStyle.Color2));
            }
            finally
            {
                this.isApplyingPreset = false;
            }
        }

        private PreviewType GetSelectedPreviewType()
        {
            // this needs to match the order of the items in the "Style" ComboBox
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

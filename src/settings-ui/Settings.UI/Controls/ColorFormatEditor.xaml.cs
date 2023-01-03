// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ColorFormatEditor : UserControl
    {
        public ColorFormatEditor()
        {
            this.InitializeComponent();
            LoadParameters();
        }

        public void LoadParameters()
        {
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            ParametersItemsControl.ItemsSource = new List<ColorFormatParameter>
            {
                new ColorFormatParameter() { Parameter = "%Re", Description = resourceLoader.GetString("Help_red") },
                new ColorFormatParameter() { Parameter = "%Gr", Description = resourceLoader.GetString("Help_green") },
                new ColorFormatParameter() { Parameter = "%Bl", Description = resourceLoader.GetString("Help_blue") },
                new ColorFormatParameter() { Parameter = "%Al", Description = resourceLoader.GetString("Help_alpha") },
                new ColorFormatParameter() { Parameter = "%Cy", Description = resourceLoader.GetString("Help_cyan") },
                new ColorFormatParameter() { Parameter = "%Ma", Description = resourceLoader.GetString("Help_magenta") },
                new ColorFormatParameter() { Parameter = "%Ye", Description = resourceLoader.GetString("Help_yellow") },
                new ColorFormatParameter() { Parameter = "%Bk", Description = resourceLoader.GetString("Help_black_key") },
                new ColorFormatParameter() { Parameter = "%Hu", Description = resourceLoader.GetString("Help_hue") },
                new ColorFormatParameter() { Parameter = "%Si", Description = resourceLoader.GetString("Help_saturationI") },
                new ColorFormatParameter() { Parameter = "%Sl", Description = resourceLoader.GetString("Help_saturationL") },
                new ColorFormatParameter() { Parameter = "%Sb", Description = resourceLoader.GetString("Help_saturationB") },
                new ColorFormatParameter() { Parameter = "%Br", Description = resourceLoader.GetString("Help_brightness") },
                new ColorFormatParameter() { Parameter = "%In", Description = resourceLoader.GetString("Help_intensity") },
                new ColorFormatParameter() { Parameter = "%Hn", Description = resourceLoader.GetString("Help_hueNat") },
                new ColorFormatParameter() { Parameter = "%Ll", Description = resourceLoader.GetString("Help_lightnessNat") },
                new ColorFormatParameter() { Parameter = "%Lc", Description = resourceLoader.GetString("Help_lightnessCIE") },
                new ColorFormatParameter() { Parameter = "%Va", Description = resourceLoader.GetString("Help_value") },
                new ColorFormatParameter() { Parameter = "%Wh", Description = resourceLoader.GetString("Help_whiteness") },
                new ColorFormatParameter() { Parameter = "%Bn", Description = resourceLoader.GetString("Help_blackness") },
                new ColorFormatParameter() { Parameter = "%Ca", Description = resourceLoader.GetString("Help_chromaticityA") },
                new ColorFormatParameter() { Parameter = "%Cb", Description = resourceLoader.GetString("Help_chromaticityB") },
                new ColorFormatParameter() { Parameter = "%Xv", Description = resourceLoader.GetString("Help_X_value") },
                new ColorFormatParameter() { Parameter = "%Yv", Description = resourceLoader.GetString("Help_Y_value") },
                new ColorFormatParameter() { Parameter = "%Zv", Description = resourceLoader.GetString("Help_Z_value") },
                new ColorFormatParameter() { Parameter = "%Dv", Description = resourceLoader.GetString("Help_decimal_value_BGR") },
                new ColorFormatParameter() { Parameter = "%Dr", Description = resourceLoader.GetString("Help_decimal_value_RGB") },
                new ColorFormatParameter() { Parameter = "%Na", Description = resourceLoader.GetString("Help_color_name") },
            };

            ColorParametersItemsControl.ItemsSource = new List<ColorFormatParameter>
            {
                new ColorFormatParameter() { Parameter = "b", Description = resourceLoader.GetString("Help_byte") },
                new ColorFormatParameter() { Parameter = "h", Description = resourceLoader.GetString("Help_hexL1") },
                new ColorFormatParameter() { Parameter = "H", Description = resourceLoader.GetString("Help_hexU1") },
                new ColorFormatParameter() { Parameter = "x", Description = resourceLoader.GetString("Help_hexL2") },
                new ColorFormatParameter() { Parameter = "X", Description = resourceLoader.GetString("Help_hexU2") },
                new ColorFormatParameter() { Parameter = "f", Description = resourceLoader.GetString("Help_floatWith") },
                new ColorFormatParameter() { Parameter = "F", Description = resourceLoader.GetString("Help_floatWithout") },
            };
        }

        private void NewColorName_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnPropertyChanged();
        }

        private void NewColorFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OnPropertyChanged();
        }

        public event EventHandler PropertyChanged;

        private void OnPropertyChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PropertyChanged"));
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class ColorFormatParameter
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Parameter { get; set; }

        public string Description { get; set; }
    }
}

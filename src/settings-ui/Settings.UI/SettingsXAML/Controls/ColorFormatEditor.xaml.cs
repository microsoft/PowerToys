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
using Microsoft.Windows.ApplicationModel.Resources;
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
            ParametersItemsControl.ItemsSource = new List<ColorFormatParameter>
            {
                new ColorFormatParameter() { Parameter = "%Re", Description = LocalizerInstance.Instance.GetLocalizedString("Help_red") },
                new ColorFormatParameter() { Parameter = "%Gr", Description = LocalizerInstance.Instance.GetLocalizedString("Help_green") },
                new ColorFormatParameter() { Parameter = "%Bl", Description = LocalizerInstance.Instance.GetLocalizedString("Help_blue") },
                new ColorFormatParameter() { Parameter = "%Al", Description = LocalizerInstance.Instance.GetLocalizedString("Help_alpha") },
                new ColorFormatParameter() { Parameter = "%Cy", Description = LocalizerInstance.Instance.GetLocalizedString("Help_cyan") },
                new ColorFormatParameter() { Parameter = "%Ma", Description = LocalizerInstance.Instance.GetLocalizedString("Help_magenta") },
                new ColorFormatParameter() { Parameter = "%Ye", Description = LocalizerInstance.Instance.GetLocalizedString("Help_yellow") },
                new ColorFormatParameter() { Parameter = "%Bk", Description = LocalizerInstance.Instance.GetLocalizedString("Help_black_key") },
                new ColorFormatParameter() { Parameter = "%Hu", Description = LocalizerInstance.Instance.GetLocalizedString("Help_hue") },
                new ColorFormatParameter() { Parameter = "%Si", Description = LocalizerInstance.Instance.GetLocalizedString("Help_saturationI") },
                new ColorFormatParameter() { Parameter = "%Sl", Description = LocalizerInstance.Instance.GetLocalizedString("Help_saturationL") },
                new ColorFormatParameter() { Parameter = "%Sb", Description = LocalizerInstance.Instance.GetLocalizedString("Help_saturationB") },
                new ColorFormatParameter() { Parameter = "%Br", Description = LocalizerInstance.Instance.GetLocalizedString("Help_brightness") },
                new ColorFormatParameter() { Parameter = "%In", Description = LocalizerInstance.Instance.GetLocalizedString("Help_intensity") },
                new ColorFormatParameter() { Parameter = "%Hn", Description = LocalizerInstance.Instance.GetLocalizedString("Help_hueNat") },
                new ColorFormatParameter() { Parameter = "%Ll", Description = LocalizerInstance.Instance.GetLocalizedString("Help_lightnessNat") },
                new ColorFormatParameter() { Parameter = "%Lc", Description = LocalizerInstance.Instance.GetLocalizedString("Help_lightnessCIE") },
                new ColorFormatParameter() { Parameter = "%Va", Description = LocalizerInstance.Instance.GetLocalizedString("Help_value") },
                new ColorFormatParameter() { Parameter = "%Wh", Description = LocalizerInstance.Instance.GetLocalizedString("Help_whiteness") },
                new ColorFormatParameter() { Parameter = "%Bn", Description = LocalizerInstance.Instance.GetLocalizedString("Help_blackness") },
                new ColorFormatParameter() { Parameter = "%Ca", Description = LocalizerInstance.Instance.GetLocalizedString("Help_chromaticityA") },
                new ColorFormatParameter() { Parameter = "%Cb", Description = LocalizerInstance.Instance.GetLocalizedString("Help_chromaticityB") },
                new ColorFormatParameter() { Parameter = "%Xv", Description = LocalizerInstance.Instance.GetLocalizedString("Help_X_value") },
                new ColorFormatParameter() { Parameter = "%Yv", Description = LocalizerInstance.Instance.GetLocalizedString("Help_Y_value") },
                new ColorFormatParameter() { Parameter = "%Zv", Description = LocalizerInstance.Instance.GetLocalizedString("Help_Z_value") },
                new ColorFormatParameter() { Parameter = "%Dv", Description = LocalizerInstance.Instance.GetLocalizedString("Help_decimal_value_BGR") },
                new ColorFormatParameter() { Parameter = "%Dr", Description = LocalizerInstance.Instance.GetLocalizedString("Help_decimal_value_RGB") },
                new ColorFormatParameter() { Parameter = "%Na", Description = LocalizerInstance.Instance.GetLocalizedString("Help_color_name") },
            };

            ColorParametersItemsControl.ItemsSource = new List<ColorFormatParameter>
            {
                new ColorFormatParameter() { Parameter = "b", Description = LocalizerInstance.Instance.GetLocalizedString("Help_byte") },
                new ColorFormatParameter() { Parameter = "h", Description = LocalizerInstance.Instance.GetLocalizedString("Help_hexL1") },
                new ColorFormatParameter() { Parameter = "H", Description = LocalizerInstance.Instance.GetLocalizedString("Help_hexU1") },
                new ColorFormatParameter() { Parameter = "x", Description = LocalizerInstance.Instance.GetLocalizedString("Help_hexL2") },
                new ColorFormatParameter() { Parameter = "X", Description = LocalizerInstance.Instance.GetLocalizedString("Help_hexU2") },
                new ColorFormatParameter() { Parameter = "f", Description = LocalizerInstance.Instance.GetLocalizedString("Help_floatWith") },
                new ColorFormatParameter() { Parameter = "F", Description = LocalizerInstance.Instance.GetLocalizedString("Help_floatWithout") },
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

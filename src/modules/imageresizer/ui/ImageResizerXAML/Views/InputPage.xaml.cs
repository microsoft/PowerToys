// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using ImageResizer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using static ImageResizer.ViewModels.InputViewModel;

namespace ImageResizer.Views
{
    public sealed partial class InputPage : Page
    {
        public InputViewModel ViewModel { get; set; }

        public InputPage()
        {
            InitializeComponent();
        }

        private void NumberBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var numberBox = sender as NumberBox;
                if (numberBox != null && ViewModel != null)
                {
                    KeyPressParams keyParams;
                    var value = numberBox.Value;

                    if (!double.IsNaN(value))
                    {
                        switch (numberBox.Name)
                        {
                            case "WidthNumberBox":
                                keyParams = new KeyPressParams
                                {
                                    Value = value,
                                    Dimension = Dimension.Width,
                                };
                                break;

                            case "HeightNumberBox":
                                keyParams = new KeyPressParams
                                {
                                    Value = value,
                                    Dimension = Dimension.Height,
                                };
                                break;

                            default:
                                return;
                        }

                        ViewModel.EnterKeyPressedCommand.Execute(keyParams);
                    }
                }
            }
        }
    }
}

// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Windows.Controls;
using System.Windows.Input;
using ImageResizer.ViewModels;
using ModernWpf.Controls;
using static ImageResizer.ViewModels.InputViewModel;

namespace ImageResizer.Views
{
    public partial class InputPage : UserControl
    {
        public InputPage()
            => InitializeComponent();

        private void Button_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var numberBox = sender as NumberBox;
                var viewModel = (InputViewModel)this.DataContext;
                double number;
                KeyPressParams keyParams;
                if (double.TryParse(((TextBox)e.OriginalSource).Text, out number))
                {
                    switch (numberBox.Name)
                    {
                        case "WidthNumberBox":
                            keyParams = new KeyPressParams
                            {
                                Value = number,
                                Dimension = Dimension.Width,
                            };
                            break;

                        case "HeightNumberBox":
                            keyParams = new KeyPressParams
                            {
                                Value = number,
                                Dimension = Dimension.Height,
                            };
                            break;

                        default:
                            // Return without EnterKeyPressedCommand executed
                            return;
                    }

                    viewModel.EnterKeyPressedCommand.Execute(keyParams);
                }
            }
        }
    }
}

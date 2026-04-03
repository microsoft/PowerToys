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

        private void ResizeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (FocusManager.GetFocusedElement(XamlRoot) is NumberBox)
            {
                args.Handled = true;
            }
        }

        private void NumberBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter
                && sender is NumberBox numberBox
                && ViewModel is not null
                && !double.IsNaN(numberBox.Value))
            {
                KeyPressParams keyParams = numberBox.Name switch
                {
                    "WidthNumberBox" => new KeyPressParams { Value = numberBox.Value, Dimension = Dimension.Width },
                    "HeightNumberBox" => new KeyPressParams { Value = numberBox.Value, Dimension = Dimension.Height },
                    _ => null,
                };

                if (keyParams is not null)
                {
                    ViewModel.EnterKeyPressedCommand.Execute(keyParams);
                }
            }
        }
    }
}

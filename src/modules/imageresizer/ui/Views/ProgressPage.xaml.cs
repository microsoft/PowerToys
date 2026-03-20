#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ImageResizer.Views
{
    public sealed partial class ProgressPage : UserControl
    {
        public ProgressPage()
            => InitializeComponent();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ProgressViewModel vm)
            {
                vm.StartCommand.Execute(null);
            }
        }
    }
}

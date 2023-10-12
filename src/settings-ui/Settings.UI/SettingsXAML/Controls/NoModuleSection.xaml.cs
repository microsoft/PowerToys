// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class NoModuleSection : UserControl
    {
        private ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;

        public string ModuleName
        {
            get { return (string)GetValue(ModuleNameProperty); }
            set { SetValue(ModuleNameProperty, value); }
        }

        public static readonly Microsoft.UI.Xaml.DependencyProperty ModuleNameProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register("ModuleName", typeof(string), typeof(NoModuleSection), null);

        public NoModuleSection()
        {
            this.InitializeComponent();
            this.Visibility = Visibility.Collapsed;

            Loaded += NoModuleSection_Loaded;
        }

        private void NoModuleSection_Loaded(object sender, RoutedEventArgs e)
        {
            NMS_TextBlock.Text = resourceLoader.GetString("NMS_Text_Start")
                + this.ModuleName
                + resourceLoader.GetString("NMS_Text_End");
        }
    }
}

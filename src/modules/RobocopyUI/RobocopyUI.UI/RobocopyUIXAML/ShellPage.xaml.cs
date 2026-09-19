// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using RobocopyUI.Helpers;

namespace RobocopyUI
{
    public sealed partial class ShellPage : Window
    {
        public ShellPage()
        {
            InitializeComponent();

            this.Title = ResourceLoaderInstance.ResourceLoader.GetString("ShellPageWindow/Title");

            ContentFrame.Navigate(typeof(HomePage));
        }
    }
}

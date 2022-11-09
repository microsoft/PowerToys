// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Hosts.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace Hosts.Controls
{
    // A Content Dialog that respects the title bar height for ExtendsContentIntoTitleBar.
    internal class TopMarginContentDialog : ContentDialog
    {
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            try
            {
                // Based on the template from ContentDialog_themeresources.xaml in https://github.com/microsoft/microsoft-ui-xaml
                var grid = GetTemplateChild("BackgroundElement") as Border;
                grid.Margin = new Microsoft.UI.Xaml.Thickness(0, 32, 0, 0); // Should be the size reserved for the title bar as in MainWindow.xaml
            }
            catch (Exception ex)
            {
                Logger.LogError("Couldn't set the margin for a content dialog. It will appear on top of the title bar.", ex);
            }
        }
    }
}

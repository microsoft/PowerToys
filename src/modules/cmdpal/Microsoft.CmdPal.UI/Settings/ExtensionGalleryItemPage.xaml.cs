// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionGalleryItemPage : Page
{
    public ExtensionGalleryItemViewModel? ViewModel { get; private set; }

    public ExtensionGalleryItemPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ExtensionGalleryItemViewModel vm)
        {
            ViewModel = vm;
            Bindings.Update();
        }
    }

    private void StoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.InstallViaStoreCommand.Execute(null);
    }

    private async void WinGetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WinGetDialog.XamlRoot = XamlRoot;
        await WinGetDialog.ShowAsync();
    }

    private void WebMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenInstallUrlCommand.Execute(null);
    }
}

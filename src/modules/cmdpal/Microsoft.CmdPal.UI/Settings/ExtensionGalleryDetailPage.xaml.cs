// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Controls;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionGalleryDetailPage : Page
{
    public GalleryExtensionViewModel? ViewModel { get; private set; }

    public ExtensionGalleryDetailPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is GalleryExtensionViewModel vm)
        {
            ViewModel = vm;
            Bindings.Update();
        }
    }

    private async void InstallBtn_Click(object sender, RoutedEventArgs e)
    {
        // Select the first visible SegmentedItem
        foreach (var item in SourceSelector.Items)
        {
            if (item is SegmentedItem { Visibility: Visibility.Visible, Tag: string tag } segmentedItem)
            {
                SourceSelector.SelectedItem = segmentedItem;
                InstallSourcePresenter.Value = tag;
                break;
            }
        }

        InstallDialog.XamlRoot = XamlRoot;
        await InstallDialog.ShowAsync();
    }

    private void SourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Segmented segmented && segmented.SelectedItem is SegmentedItem { Tag: string tag })
        {
            InstallSourcePresenter.Value = tag;
        }
    }
}

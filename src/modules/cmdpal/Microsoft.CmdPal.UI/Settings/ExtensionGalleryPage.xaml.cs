// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.ExtensionGallery.Services;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionGalleryPage : Page, IDisposable
{
    private readonly ExtensionGalleryViewModel viewModel;

    public ExtensionGalleryPage()
    {
        var galleryService = App.Current.Services.GetService<IExtensionGalleryService>()!;
        var extensionService = App.Current.Services.GetService<IExtensionService>()!;
        var winGetPackageManagerService = App.Current.Services.GetService<IWinGetPackageManagerService>();
        var winGetOperationTrackerService = App.Current.Services.GetService<IWinGetOperationTrackerService>();
        var winGetPackageStatusService = App.Current.Services.GetService<IWinGetPackageStatusService>();
        var uiScheduler = App.Current.Services.GetService<TaskScheduler>();

        viewModel = new ExtensionGalleryViewModel(
            galleryService,
            extensionService,
            winGetPackageManagerService,
            winGetPackageStatusService,
            winGetOperationTrackerService,
            uiScheduler);

        this.InitializeComponent();

        Loaded += ExtensionGalleryPage_Loaded;
        Unloaded += ExtensionGalleryPage_Unloaded;
    }

    private void ExtensionGalleryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        viewModel.Dispose();
    }

    private async void ExtensionGalleryPage_Loaded(object sender, RoutedEventArgs e)
    {
        await viewModel.LoadAsync();
    }

    private void TileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GalleryExtensionViewModel vm)
        {
            NavigateToDetails(vm);
        }
    }

    private void OnFindInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox?.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void ViewDetailsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is GalleryExtensionViewModel vm)
        {
            NavigateToDetails(vm);
        }
    }

    private void NavigateToDetails(GalleryExtensionViewModel vm)
    {
        Frame?.Navigate(typeof(ExtensionGalleryDetailPage), vm);
    }

    public void Dispose() => viewModel.Dispose();
}

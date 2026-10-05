// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionGalleryPage : Page, IDisposable
{
    private string? _extensionIdToOpen;
    private bool _isGalleryLoaded;
    private bool _disposed;

    public ExtensionGalleryViewModel ViewModel { get; }

    public ExtensionGalleryPage()
    {
        // SettingsWindow disposes the page and its view model when navigating away.
        NavigationCacheMode = NavigationCacheMode.Disabled;
        ViewModel = App.Current.Services.GetRequiredService<ExtensionGalleryViewModel>();

        this.InitializeComponent();

        Loaded += ExtensionGalleryPage_Loaded;
    }

    private async void ExtensionGalleryPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();

        if (_disposed || !IsLoaded)
        {
            return;
        }

        _isGalleryLoaded = true;
        OpenPendingExtension();
    }

    internal void OpenExtension(string extensionId)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            return;
        }

        _extensionIdToOpen = extensionId;
        OpenPendingExtension();
    }

    internal void ClearPendingExtension()
    {
        _extensionIdToOpen = null;
    }

    private void OpenPendingExtension()
    {
        if (!IsLoaded || !_isGalleryLoaded || _extensionIdToOpen is not { } extensionId)
        {
            return;
        }

        _extensionIdToOpen = null;
        var extension = ViewModel.FindById(extensionId);
        if (extension is not null)
        {
            NavigateToDetails(extension);
        }
        else
        {
            Logger.LogWarning($"Extension gallery entry '{extensionId}' was not found.");
        }
    }

    private void GalleryItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is ExtensionGalleryItemViewModel vm)
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
        if (sender is MenuFlyoutItem item && item.Tag is ExtensionGalleryItemViewModel vm)
        {
            NavigateToDetails(vm);
        }
    }

    private void NavigateToDetails(ExtensionGalleryItemViewModel vm)
    {
        Frame?.Navigate(typeof(ExtensionGalleryItemPage), vm);
    }

    public void Dispose()
    {
        _disposed = true;
        Loaded -= ExtensionGalleryPage_Loaded;
        _extensionIdToOpen = null;
        _isGalleryLoaded = false;
        ViewModel.Dispose();
    }
}

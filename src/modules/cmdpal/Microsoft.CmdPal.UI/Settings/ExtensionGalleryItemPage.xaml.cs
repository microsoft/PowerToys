// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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

    private void ScreenshotsItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is ExtensionGalleryScreenshotViewModel screenshot)
        {
            PrepareScreenshotOpenAnimation(sender, screenshot);
            WeakReferenceMessenger.Default.Send(
                new OpenExtensionGalleryScreenshotViewerMessage(
                    ViewModel?.Screenshots ?? [],
                    screenshot));
        }
    }

    private static void PrepareScreenshotOpenAnimation(ItemsView itemsView, ExtensionGalleryScreenshotViewModel screenshot)
    {
        var repeater = FindDescendant<ItemsRepeater>(itemsView);
        var element = repeater?.TryGetElement(screenshot.Index);
        if (element is UIElement sourceElement)
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(OpenExtensionGalleryScreenshotViewerMessage.ConnectedAnimationKey, sourceElement);
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
            {
                return found;
            }

            var result = FindDescendant<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}

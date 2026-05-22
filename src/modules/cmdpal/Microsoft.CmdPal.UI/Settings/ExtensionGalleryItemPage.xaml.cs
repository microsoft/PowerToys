// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionGalleryItemPage : Page
{
    public ExtensionGalleryItemViewModel? ViewModel { get; private set; }

    public ExtensionGalleryItemPage()
    {
        Logger.LogDebug("[ItemPage] .ctor");
        this.InitializeComponent();
        Logger.LogDebug("[ItemPage] InitializeComponent done");

        Loading += (_, _) => Logger.LogDebug("[ItemPage] Loading");
        Loaded += (_, _) => Logger.LogDebug("[ItemPage] Loaded");
        SizeChanged += (_, args) => Logger.LogDebug($"[ItemPage] SizeChanged: {args.NewSize.Width}x{args.NewSize.Height}");
        LayoutUpdated += (_, _) => Logger.LogDebug("[ItemPage] LayoutUpdated");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Logger.LogDebug($"[ItemPage] OnNavigatedTo entry, mode={e.NavigationMode}, paramType={e.Parameter?.GetType().FullName ?? "null"}");
        base.OnNavigatedTo(e);
        if (e.Parameter is ExtensionGalleryItemViewModel vm)
        {
            Logger.LogDebug($"[ItemPage] Setting ViewModel: id={vm.Id}, title={vm.DisplayTitle}, iconUri={vm.IconUri}, homepage={vm.Homepage ?? "<null>"}, homepageUri={vm.HomepageUri?.AbsoluteUri ?? "<null>"}, screenshots={vm.Screenshots.Count}, sourcesWithDetails={vm.SourcesWithDetails.Count}");
            ViewModel = vm;
            Logger.LogDebug("[ItemPage] Calling Bindings.Update()");
            try
            {
                Bindings.Update();
                Logger.LogDebug("[ItemPage] Bindings.Update() returned");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ItemPage] Bindings.Update() threw: {ex.GetType().FullName}: {ex.Message}", ex);
                throw;
            }
        }

        Logger.LogDebug("[ItemPage] OnNavigatedTo exit");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        try
        {
            return base.MeasureOverride(availableSize);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ItemPage] MeasureOverride threw: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}", ex);
            throw;
        }
    }

    private void StoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.InstallViaStoreCommand.Execute(null);
    }

    private async void WinGetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // WinGetDialog is declared with x:Load="False" so that its template and
        // bindings aren't realized as part of the page's initial measure pass
        // (which crashes in AOT/trimmed builds). Realize it on first use via
        // FindName, then show it.
        if (WinGetDialog is null)
        {
            FindName(nameof(WinGetDialog));
        }

        if (WinGetDialog is null)
        {
            return;
        }

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

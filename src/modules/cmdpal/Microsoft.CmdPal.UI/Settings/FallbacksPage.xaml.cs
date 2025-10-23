// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class FallbacksPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;

    public FallbacksPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        viewModel = new SettingsViewModel(settings, App.Current.Services, _mainTaskScheduler);
    }

    private void SettingsCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is SettingsCard card)
        {
            if (card.DataContext is ProviderSettingsViewModel vm)
            {
                WeakReferenceMessenger.Default.Send<NavigateToExtensionSettingsMessage>(new(vm));
            }
        }
    }

    private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move &&
            args.Items.Count > 0)
        {
            var item = args.Items[0];
            if (item is FallbackSettingsViewModel droppedCommand)
            {
                viewModel?.ReorderFallbacks(droppedCommand, sender.Items.Cast<FallbackSettingsViewModel>().ToList());
            }
        }
    }
}

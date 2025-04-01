// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;

    public ExtensionsPage()
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
}

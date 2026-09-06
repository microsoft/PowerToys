// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionPage : Page, IDisposable
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    public ProviderSettingsViewModel? ViewModel { get; private set; }

    public ExtensionPage()
    {
        this.InitializeComponent();
    }

    public void Dispose()
    {
        ViewModel?.Dispose();
        FallbackRankerDialog?.Dispose();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var provider = e.Parameter as CommandProviderWrapper
            ?? throw new ArgumentException($"{nameof(ExtensionPage)} navigation args should be passed a {nameof(CommandProviderWrapper)}");
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();

        var settings = settingsService.Settings;
        var (model, providerSettings) = settings.GetProviderSettings(provider);
        if (!ReferenceEquals(model, settings))
        {
            settingsService.UpdateSettings(current => current.GetProviderSettings(provider).Model, hotReload: false);
            providerSettings = settingsService.Settings.ProviderSettings[provider.ProviderId];
        }

        // Navigation retains the shared provider, while each page owns its own settings view model.
        ViewModel = new ProviderSettingsViewModel(provider, providerSettings, settingsService);
    }

    private async void RankButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await FallbackRankerDialog.ShowAsync();
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
    private readonly FallbackRankerDialog _fallbackRankerDialog;

    public ProviderSettingsViewModel? ViewModel { get; private set; }

    private ContentPage _contentPage;

    public ExtensionPage(
        FallbackRankerDialog fallbackRankerDialog,
        ImageProvider imageProvider)
    {
        this.InitializeComponent();
        _fallbackRankerDialog = fallbackRankerDialog;

        FallbackRankerContainer.Content = _fallbackRankerDialog;

        _contentPage = new ContentPage(imageProvider);
        ContentPageContainer.Content = _contentPage;
    }

    internal void OnNavigatedTo(ProviderSettingsViewModel viewModel)
    {
        if (viewModel is not ProviderSettingsViewModel)
        {
            throw new ArgumentException($"{nameof(ExtensionPage)} navigation args should be passed a {nameof(ProviderSettingsViewModel)}");
        }

        ViewModel = viewModel;

        _contentPage.SetBinding(
           ContentPage.ViewModelProperty,
           new Binding
           {
               Source = ViewModel.SettingsPage,
               Mode = BindingMode.OneWay,
           });
    }

    private async void RankButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await _fallbackRankerDialog.ShowAsync();
    }
}

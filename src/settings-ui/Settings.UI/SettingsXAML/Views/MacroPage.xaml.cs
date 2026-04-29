// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class MacroPage : NavigablePage
{
    public MacroViewModel ViewModel { get; }

    public MacroPage()
    {
        ViewModel = new MacroViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void NewMacro_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SuspendHotkeysAsync();
        try
        {
            var editVm = new MacroEditViewModel();
            var dialog = new MacroEditDialog(editVm) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !editVm.HasValidationError)
            {
                await ViewModel.SaveMacroAsync(editVm);
            }
        }
        finally
        {
            await ViewModel.ResumeHotkeysAsync();
        }
    }

    private async void EditMacro_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MacroListItem item })
        {
            return;
        }

        await ViewModel.SuspendHotkeysAsync();
        try
        {
            var editVm = new MacroEditViewModel(item.Definition);
            var dialog = new MacroEditDialog(editVm) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !editVm.HasValidationError)
            {
                await ViewModel.SaveMacroAsync(editVm);
            }
        }
        finally
        {
            await ViewModel.ResumeHotkeysAsync();
        }
    }

    private async void DeleteMacro_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MacroListItem item })
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Delete macro?",
            Content = $"'{item.Name}' will be permanently deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        var result = await confirm.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.DeleteMacro(item);
        }
    }
}

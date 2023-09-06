// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Labs.WinUI;
using CommunityToolkit.Mvvm.Input;
using EnvironmentVariables.Models;
using EnvironmentVariables.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace EnvironmentVariables.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public ICommand EditCommand => new RelayCommand<Variable>(EditVariable);

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<MainViewModel>();
            DataContext = ViewModel;
        }

        private async Task ShowEditDialogAsync(Variable variable)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            EditDialog.Title = resourceLoader.GetString("EditVariableDialog_Title");
            EditDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
            EditDialog.PrimaryButtonCommand = EditCommand;
            EditDialog.PrimaryButtonCommandParameter = variable;

            var clone = variable.Clone();
            EditDialog.DataContext = clone;

            await EditDialog.ShowAsync();
        }

        private async void EditVariable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingsCard card = sender as SettingsCard;
            if (card != null)
            {
                await ShowEditDialogAsync(card.CommandParameter as Variable);
            }
        }

        private void EditVariable(Variable original)
        {
            var edited = EditDialog.DataContext as Variable;
            ViewModel.EditVariable(original, edited);
        }
    }
}

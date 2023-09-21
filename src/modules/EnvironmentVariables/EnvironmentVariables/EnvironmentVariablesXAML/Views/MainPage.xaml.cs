// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Controls;
using EnvironmentVariables.Models;
using EnvironmentVariables.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace EnvironmentVariables.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public ICommand EditCommand => new RelayCommand<SettingsCard>(EditVariable);

        public ICommand NewProfileCommand => new AsyncRelayCommand(AddProfileAsync);

        public ICommand AddProfileCommand => new RelayCommand(AddProfile);

        public ICommand AddVariableCommand => new RelayCommand(AddVariable);

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<MainViewModel>();
            DataContext = ViewModel;
        }

        private async Task ShowEditDialogAsync(SettingsCard card)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            EditVariableDialog.Title = resourceLoader.GetString("EditVariableDialog_Title");
            EditVariableDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
            EditVariableDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
            EditVariableDialog.PrimaryButtonCommand = EditCommand;
            EditVariableDialog.PrimaryButtonCommandParameter = card;

            var variable = card.CommandParameter as Variable;
            var clone = variable.Clone();
            EditVariableDialog.DataContext = clone;

            await EditVariableDialog.ShowAsync();
        }

        private async void EditVariable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingsCard card = sender as SettingsCard;
            if (card != null)
            {
                await ShowEditDialogAsync(card);
            }
        }

        private void EditVariable(SettingsCard card)
        {
            var variableSet = card.DataContext as ProfileVariablesSet;
            var original = card.CommandParameter as Variable;
            var edited = EditVariableDialog.DataContext as Variable;
            ViewModel.EditVariable(original, edited, variableSet);
        }

        private async Task AddProfileAsync()
        {
            SwitchViewsSegmentedView.SelectedIndex = 0;

            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            AddProfileDialog.Title = resourceLoader.GetString("AddNewProfileDialog_Title");
            AddProfileDialog.PrimaryButtonText = resourceLoader.GetString("AddBtn");
            AddProfileDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
            AddProfileDialog.PrimaryButtonCommand = AddProfileCommand;
            AddProfileDialog.DataContext = new ProfileVariablesSet(Guid.NewGuid(), string.Empty);
            await AddProfileDialog.ShowAsync();
        }

        private void AddProfile()
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            ViewModel.AddProfile(profile);
        }

        private async void RemoveProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button.CommandParameter as ProfileVariablesSet;

            if (profile != null)
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = RootPage.XamlRoot;
                dialog.Title = profile.Name;
                dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
                dialog.CloseButtonText = resourceLoader.GetString("No");
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description") };
                dialog.PrimaryButtonClick += (s, args) =>
                {
                    ViewModel.RemoveProfile(profile);
                };

                var result = await dialog.ShowAsync();
            }
        }

        private void AddVariable()
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (profile != null)
            {
                if (AddVariableSwitchPresenter.Value as string == "NewVariable")
                {
                    profile.Variables.Add(new Variable(AddNewVariableName.Text, AddNewVariableValue.Text, VariablesSetType.Profile));
                }
                else
                {
                    foreach (Variable variable in ExistingVariablesListView.SelectedItems)
                    {
                        var clone = variable.Clone(true);
                        profile.Variables.Add(clone);
                    }
                }
            }

            AddNewVariableName.Text = string.Empty;
            AddNewVariableValue.Text = string.Empty;
            ExistingVariablesListView.SelectedItems.Clear();
            AddVariableFlyout.Hide();
        }

        private void Delete_Variable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            MenuFlyoutItem menuItem = sender as MenuFlyoutItem;
            var variableSet = menuItem.DataContext as ProfileVariablesSet;
            var variable = menuItem.CommandParameter as Variable;

            if (variable != null)
            {
                ViewModel.DeleteVariable(variable, variableSet);
            }
        }

        private void AddNewVariableName_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox nameTxtBox = sender as TextBox;
            if (nameTxtBox != null)
            {
                if (nameTxtBox.Text.Length > 0)
                {
                    ConfirmAddVariableBtn.IsEnabled = true;
                }
                else
                {
                    ConfirmAddVariableBtn.IsEnabled = false;
                }
            }
        }
    }
}

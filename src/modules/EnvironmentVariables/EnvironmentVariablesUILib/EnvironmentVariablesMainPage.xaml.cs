// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace EnvironmentVariablesUILib
{
    public sealed partial class EnvironmentVariablesMainPage : Page
    {
        private sealed class RelayCommandParameter
        {
            public RelayCommandParameter(Variable variable, VariablesSet set)
            {
                Variable = variable;
                this.Set = set;
            }

            public Variable Variable { get; set; }

            public VariablesSet Set { get; set; }
        }

        public MainViewModel ViewModel { get; private set; }

        public ICommand EditCommand => new RelayCommand<RelayCommandParameter>(EditVariable);

        public ICommand NewProfileCommand => new AsyncRelayCommand(AddProfileAsync);

        public ICommand AddProfileCommand => new RelayCommand(AddProfile);

        public ICommand UpdateProfileCommand => new RelayCommand(UpdateProfile);

        public ICommand AddVariableCommand => new RelayCommand(AddVariable);

        public ICommand CancelAddVariableCommand => new RelayCommand(CancelAddVariable);

        public ICommand AddDefaultVariableCommand => new RelayCommand<DefaultVariablesSet>(AddDefaultVariable);

        public EnvironmentVariablesMainPage(MainViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            ViewModel.LoadEnvironmentVariables();
        }

        private async Task ShowEditDialogAsync(Variable variable, VariablesSet parentSet)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            EditVariableDialog.Title = resourceLoader.GetString("EditVariableDialog_Title");
            EditVariableDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
            EditVariableDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
            EditVariableDialog.PrimaryButtonCommand = EditCommand;
            EditVariableDialog.PrimaryButtonCommandParameter = new RelayCommandParameter(variable, parentSet);

            var clone = variable.Clone();
            EditVariableDialog.DataContext = clone;

            await EditVariableDialog.ShowAsync();
        }

        private async void EditVariable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var btn = sender as MenuFlyoutItem;
            var variablesSet = btn.DataContext as VariablesSet;
            var variable = btn.CommandParameter as Variable;

            if (variable != null)
            {
                await ShowEditDialogAsync(variable, variablesSet);
            }
        }

        private void EditVariable(RelayCommandParameter param)
        {
            var variableSet = param.Set as ProfileVariablesSet;
            var original = param.Variable;
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

        private void UpdateProfile()
        {
            var updatedProfile = AddProfileDialog.DataContext as ProfileVariablesSet;
            ViewModel.UpdateProfile(updatedProfile);
        }

        private async void RemoveProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as MenuFlyoutItem;
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
                dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description"), TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords };
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
                        if (!profile.Variables.Where(x => x.Name == variable.Name).Any())
                        {
                            var clone = variable.Clone(true);
                            profile.Variables.Add(clone);
                        }
                    }
                }
            }

            AddNewVariableName.Text = string.Empty;
            AddNewVariableValue.Text = string.Empty;
            ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
            ExistingVariablesListView.SelectedItems.Clear();
            ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
            AddVariableFlyout.Hide();
        }

        private void CancelAddVariable()
        {
            AddNewVariableName.Text = string.Empty;
            AddNewVariableValue.Text = string.Empty;

            ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
            ExistingVariablesListView.SelectedItems.Clear();
            ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;

            AddVariableFlyout.Hide();
        }

        private void AddDefaultVariable(DefaultVariablesSet set)
        {
            var variable = AddDefaultVariableDialog.DataContext as Variable;
            var type = set.Type;

            ViewModel.AddDefaultVariable(variable, type);
        }

        private async void Delete_Variable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            MenuFlyoutItem selectedItem = sender as MenuFlyoutItem;
            var variableSet = selectedItem.DataContext as ProfileVariablesSet;
            var variable = selectedItem.CommandParameter as Variable;

            if (variable != null)
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = RootPage.XamlRoot;
                dialog.Title = variable.Name;
                dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
                dialog.CloseButtonText = resourceLoader.GetString("No");
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Variable_Description"), TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords };
                dialog.PrimaryButtonClick += (s, args) =>
                {
                    ViewModel.DeleteVariable(variable, variableSet);
                };
                var result = await dialog.ShowAsync();
            }
        }

        private void AddNewVariableName_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox nameTxtBox = sender as TextBox;
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;

            if (nameTxtBox != null)
            {
                if (nameTxtBox.Text.Length == 0 || nameTxtBox.Text.Length >= 255 || profile.Variables.Where(x => x.Name.Equals(nameTxtBox.Text, StringComparison.OrdinalIgnoreCase)).Any())
                {
                    ConfirmAddVariableBtn.IsEnabled = false;
                }
                else
                {
                    ConfirmAddVariableBtn.IsEnabled = true;
                }
            }
        }

        private void ReloadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.LoadEnvironmentVariables();
            ViewModel.EnvironmentState = EnvironmentState.Unchanged;
        }

        private void ExistingVariablesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;

            int toRemove = -1;

            if (e.AddedItems.Count > 0)
            {
                var list = sender as ListView;
                var duplicates = list.SelectedItems.GroupBy(x => ((Variable)x).Name.ToLowerInvariant()).Where(g => g.Count() > 1).ToList();

                foreach (var dup in duplicates)
                {
                    ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
                    list.SelectedItems.Remove(dup.ElementAt(1));
                    ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
                }
            }

            if (e.RemovedItems.Count > 0)
            {
                Variable removedVariable = e.RemovedItems[0] as Variable;
                for (int i = 0; i < profile.Variables.Count; i++)
                {
                    if (profile.Variables[i].Name == removedVariable.Name && profile.Variables[i].Values == removedVariable.Values)
                    {
                        toRemove = i;
                        break;
                    }
                }

                if (toRemove != -1)
                {
                    profile.Variables.RemoveAt(toRemove);
                }
            }

            ConfirmAddVariableBtn.IsEnabled = false;
            foreach (Variable variable in ExistingVariablesListView.SelectedItems)
            {
                if (variable != null)
                {
                    if (!profile.Variables.Where(x => x.Name.Equals(variable.Name, StringComparison.Ordinal) && x.Values.Equals(variable.Values, StringComparison.Ordinal)).Any())
                    {
                        ConfirmAddVariableBtn.IsEnabled = true;
                        break;
                    }
                }
            }
        }

        private async void EditProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SwitchViewsSegmentedView.SelectedIndex = 0;

            var button = sender as MenuFlyoutItem;
            var profile = button.CommandParameter as ProfileVariablesSet;

            if (profile != null)
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                AddProfileDialog.Title = resourceLoader.GetString("EditProfileDialog_Title");
                AddProfileDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
                AddProfileDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
                AddProfileDialog.PrimaryButtonCommand = UpdateProfileCommand;
                AddProfileDialog.DataContext = profile.Clone();
                await AddProfileDialog.ShowAsync();
            }
        }

        private void ExistingVariablesListView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;

            foreach (Variable item in ExistingVariablesListView.Items)
            {
                if (item != null)
                {
                    foreach (var profileItem in profile.Variables)
                    {
                        if (profileItem.Name == item.Name && profileItem.Values == item.Values)
                        {
                            if (ExistingVariablesListView.SelectedItems.Where(x => ((Variable)x).Name.Equals(profileItem.Name, StringComparison.OrdinalIgnoreCase)).Any())
                            {
                                continue;
                            }

                            ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
                            ExistingVariablesListView.SelectedItems.Add(item);
                            ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
                        }
                    }
                }
            }
        }

        private async Task ShowAddDefaultVariableDialogAsync(DefaultVariablesSet set)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            AddDefaultVariableDialog.Title = resourceLoader.GetString("AddVariable_Title");
            AddDefaultVariableDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
            AddDefaultVariableDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
            AddDefaultVariableDialog.PrimaryButtonCommand = AddDefaultVariableCommand;
            AddDefaultVariableDialog.PrimaryButtonCommandParameter = set;

            var variableType = set.Id == VariablesSet.SystemGuid ? VariablesSetType.System : VariablesSetType.User;
            AddDefaultVariableDialog.DataContext = new Variable(string.Empty, string.Empty, variableType);

            await AddDefaultVariableDialog.ShowAsync();
        }

        private async void AddDefaultVariableBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as Button;
            var defaultVariableSet = button.CommandParameter as DefaultVariablesSet;

            if (defaultVariableSet != null)
            {
                await ShowAddDefaultVariableDialogAsync(defaultVariableSet);
            }
        }

        private void EditVariableDialogNameTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var variable = EditVariableDialog.DataContext as Variable;
            var param = EditVariableDialog.PrimaryButtonCommandParameter as RelayCommandParameter;
            var variableSet = param.Set;

            if (variableSet == null)
            {
                // default set
                variableSet = variable.ParentType == VariablesSetType.User ? ViewModel.UserDefaultSet : ViewModel.SystemDefaultSet;
            }

            if (variableSet != null)
            {
                if (variableSet.Variables.Where(x => x.Name.Equals(EditVariableDialogNameTxtBox.Text, StringComparison.OrdinalIgnoreCase)).Any() || !variable.Valid)
                {
                    EditVariableDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    EditVariableDialog.IsPrimaryButtonEnabled = true;
                }
            }

            if (!variable.Validate())
            {
                EditVariableDialog.IsPrimaryButtonEnabled = false;
            }
        }

        private void AddDefaultVariableNameTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox nameTxtBox = sender as TextBox;
            var variable = AddDefaultVariableDialog.DataContext as Variable;
            var defaultSet = variable.ParentType == VariablesSetType.User ? ViewModel.UserDefaultSet : ViewModel.SystemDefaultSet;

            if (nameTxtBox != null)
            {
                if (nameTxtBox.Text.Length == 0 || defaultSet.Variables.Where(x => x.Name.Equals(nameTxtBox.Text, StringComparison.OrdinalIgnoreCase)).Any())
                {
                    AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    AddDefaultVariableDialog.IsPrimaryButtonEnabled = true;
                }
            }

            if (!variable.Validate())
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
            }
        }

        private void EditVariableDialogValueTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txtBox = sender as TextBox;
            var variable = EditVariableDialog.DataContext as Variable;
            EditVariableDialog.IsPrimaryButtonEnabled = true;

            variable.ValuesList = Variable.ValuesStringToValuesListItemCollection(txtBox.Text);
        }

        private void ReorderButtonUp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = ((MenuFlyoutItem)sender).DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;

            var index = variable.ValuesList.IndexOf(listItem);
            if (index > 0)
            {
                variable.ValuesList.Move(index, index - 1);
            }

            var newValues = string.Join(";", variable.ValuesList?.Select(x => x.Text).ToArray());
            EditVariableDialogValueTxtBox.Text = newValues;
        }

        private void ReorderButtonDown_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = ((MenuFlyoutItem)sender).DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            var btn = EditVariableDialog.PrimaryButtonCommandParameter as Button;

            var index = variable.ValuesList.IndexOf(listItem);
            if (index < variable.ValuesList.Count - 1)
            {
                variable.ValuesList.Move(index, index + 1);
            }

            var newValues = string.Join(";", variable.ValuesList?.Select(x => x.Text).ToArray());
            EditVariableDialogValueTxtBox.Text = newValues;
        }

        private void RemoveListVariableButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = ((MenuFlyoutItem)sender).DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            variable.ValuesList.Remove(listItem);

            var newValues = string.Join(";", variable.ValuesList?.Select(x => x.Text).ToArray());
            EditVariableDialogValueTxtBox.Text = newValues;
        }

        private void InsertListEntryBeforeButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as MenuFlyoutItem)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            var index = variable.ValuesList.IndexOf(listItem);
            variable.ValuesList.Insert(index, new Variable.ValuesListItem { Text = string.Empty });

            var newValues = string.Join(";", variable.ValuesList?.Select(x => x.Text).ToArray());
            EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged;
            EditVariableDialogValueTxtBox.Text = newValues;
            EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged;
        }

        private void InsertListEntryAfterButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as MenuFlyoutItem)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            var index = variable.ValuesList.IndexOf(listItem);
            variable.ValuesList.Insert(index + 1, new Variable.ValuesListItem { Text = string.Empty });

            var newValues = string.Join(";", variable.ValuesList?.Select(x => x.Text).ToArray());
            EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged;
            EditVariableDialogValueTxtBox.Text = newValues;
            EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged;
        }

        private void EditVariableValuesListTextBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as TextBox)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            if (listItem.Text == (sender as TextBox)?.Text)
            {
                return;
            }

            listItem.Text = (sender as TextBox)?.Text;
            var variable = EditVariableDialog.DataContext as Variable;

            var newValues = string.Join(";", variable.ValuesList?.Select(x => x.Text).ToArray());
            EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged;
            EditVariableDialogValueTxtBox.Text = newValues;
            EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged;
        }

        private void InvalidStateInfoBar_CloseButtonClick(InfoBar sender, object args)
        {
            ViewModel.EnvironmentState = EnvironmentState.Unchanged;
        }

        private void AddVariableFlyout_Closed(object sender, object e)
        {
            CancelAddVariable();
            ConfirmAddVariableBtn.IsEnabled = false;
        }
    }
}

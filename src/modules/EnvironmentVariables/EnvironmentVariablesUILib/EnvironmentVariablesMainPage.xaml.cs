// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;

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
                        if (variable != null && !profile.Variables
                            .Where(x => AreEquivalentVariables(x, variable))
                            .Any())
                        {
                            var clone = variable.Clone(true);
                            profile.Variables.Add(clone);
                        }
                    }
                }
            }

            ResetAddVariableInputs();
            AddVariableDialog.Hide();
        }

        private void CancelAddVariable()
        {
            ResetAddVariableInputs();
            AddVariableDialog.Hide();
        }

        private void ClearAddVariableSearchFilter()
        {
            if (DefaultVariablesSearchBox != null)
            {
                DefaultVariablesSearchBox.TextChanged -= DefaultVariablesSearchBox_TextChanged;
                DefaultVariablesSearchBox.Text = string.Empty;
                DefaultVariablesSearchBox.TextChanged += DefaultVariablesSearchBox_TextChanged;
            }

            ViewModel.ClearDefaultVariablesFilter();
            UpdateNoMatchingDefaultVariablesText();
        }

        private void UpdateNoMatchingDefaultVariablesText()
        {
            if (NoMatchingDefaultVariablesText == null || DefaultVariablesSearchBox == null)
            {
                return;
            }

            var hasActiveFilter = !string.IsNullOrWhiteSpace(DefaultVariablesSearchBox.Text);
            if (hasActiveFilter && ViewModel.FilteredDefaultVariables.Count == 0)
            {
                NoMatchingDefaultVariablesText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                NoMatchingDefaultVariablesText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }

        private void ResetAddVariableInputs()
        {
            AddNewVariableName.Text = string.Empty;
            AddNewVariableValue.Text = string.Empty;
            AddVariableDialog.IsPrimaryButtonEnabled = false;
            ClearAddVariableSearchFilter();

            ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
            ExistingVariablesListView.SelectedItems.Clear();
            ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
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

        private void CopyVariableName_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var selectedItem = sender as MenuFlyoutItem;
            var variable = selectedItem?.CommandParameter as Variable;
            if (variable == null)
            {
                return;
            }

            CopyToClipboard(variable.Name);
        }

        private void CopyVariableValue_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var selectedItem = sender as MenuFlyoutItem;
            var variable = selectedItem?.CommandParameter as Variable;
            if (variable == null)
            {
                return;
            }

            CopyToClipboard(variable.Values);
        }

        private static void CopyToClipboard(string text)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text ?? string.Empty);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }
            catch
            {
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
                    AddVariableDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    AddVariableDialog.IsPrimaryButtonEnabled = true;
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
            if (profile == null)
            {
                return;
            }

            int toRemove = -1;

            if (e.AddedItems.Count > 0)
            {
                var list = sender as ListView;
                if (list == null)
                {
                    return;
                }

                var duplicates = list.SelectedItems
                    .OfType<Variable>()
                    .GroupBy(x => $"{x.Name?.ToUpperInvariant()}|{x.Values}|{x.ParentType}")
                    .Where(g => g.Count() > 1)
                    .ToList();

                foreach (var dup in duplicates)
                {
                    ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
                    var duplicatedItems = dup
                        .OrderBy(x => list.SelectedItems.IndexOf(x))
                        .Skip(1)
                        .ToList();
                    foreach (var item in duplicatedItems)
                    {
                        list.SelectedItems.Remove(item);
                    }
                    ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
                }
            }

            if (e.RemovedItems.Count > 0)
            {
                foreach (var removedObject in e.RemovedItems)
                {
                    var removedVariable = removedObject as Variable;
                    if (removedVariable == null)
                    {
                        continue;
                    }

                    toRemove = -1;
                    for (int i = 0; i < profile.Variables.Count; i++)
                    {
                        if (AreEquivalentVariables(profile.Variables[i], removedVariable))
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
            }

            AddVariableDialog.IsPrimaryButtonEnabled = false;
            foreach (Variable variable in ExistingVariablesListView.SelectedItems)
            {
                if (variable != null)
                {
                    if (!profile.Variables
                        .Where(x => AreEquivalentVariables(x, variable))
                        .Any())
                    {
                        AddVariableDialog.IsPrimaryButtonEnabled = true;
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

        private async void AddVariableBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            AddVariableDialog.Title = resourceLoader.GetString("AddVariable_Title");
            AddVariableDialog.PrimaryButtonText = resourceLoader.GetString("AddBtn");
            AddVariableDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
            AddVariableDialog.PrimaryButtonCommand = AddVariableCommand;
            AddVariableDialog.SecondaryButtonCommand = CancelAddVariableCommand;
            ResetAddVariableInputs();
            SwitchViewsSegmentedView.SelectedIndex = 0;
            AddVariableDialog.DataContext = AddProfileDialog.DataContext;
            await AddVariableDialog.ShowAsync();
        }

        private void DefaultVariablesSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = (sender as TextBox)?.Text;
            ViewModel.SetDefaultVariablesFilter(searchText);

            ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
            ExistingVariablesListView.SelectedItems.Clear();
            ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;

            AddVariableDialog.IsPrimaryButtonEnabled = false;
            UpdateNoMatchingDefaultVariablesText();
        }

        private void DefaultVariablesSearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape && !string.IsNullOrEmpty(DefaultVariablesSearchBox.Text))
            {
                e.Handled = true;
                ClearAddVariableSearchFilter();
                DefaultVariablesSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private void ExistingVariablesListView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (profile == null)
            {
                return;
            }

            var remainingItems = ExistingVariablesListView.Items.Cast<Variable>().ToList();

            foreach (Variable item in ExistingVariablesListView.Items)
            {
                if (item != null)
                {
                    foreach (var profileItem in profile.Variables)
                    {
                        if (AreEquivalentVariables(profileItem, item))
                        {
                            if (!remainingItems.Remove(item))
                            {
                                continue;
                            }

                            ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
                            ExistingVariablesListView.SelectedItems.Add(item);
                            ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
                            break;
                        }
                    }
                }
            }

            UpdateNoMatchingDefaultVariablesText();
        }

        private static bool AreEquivalentVariables(Variable left, Variable right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Values, right.Values, StringComparison.Ordinal)
                && left.ParentType == right.ParentType;
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
            if (txtBox == null || variable == null)
            {
                return;
            }

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
            if (variable?.ValuesList == null)
            {
                return;
            }

            var index = variable.ValuesList.IndexOf(listItem);
            if (index > 0)
            {
                variable.ValuesList.Move(index, index - 1);
            }

            RefreshEditVariableDialogValueTextFromList(variable);
        }

        private void ReorderButtonDown_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = ((MenuFlyoutItem)sender).DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            if (variable?.ValuesList == null)
            {
                return;
            }

            var index = variable.ValuesList.IndexOf(listItem);
            if (index < variable.ValuesList.Count - 1)
            {
                variable.ValuesList.Move(index, index + 1);
            }

            RefreshEditVariableDialogValueTextFromList(variable);
        }

        private void RemoveListVariableButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = ((MenuFlyoutItem)sender).DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            if (variable?.ValuesList == null)
            {
                return;
            }

            variable.ValuesList.Remove(listItem);

            RefreshEditVariableDialogValueTextFromList(variable);
        }

        private void RemoveListVariableDuplicatesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var variable = EditVariableDialog.DataContext as Variable;
            if (variable?.ValuesList == null)
            {
                return;
            }

            var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasDuplicates = false;
            var hasValueChanges = false;

            for (int i = variable.ValuesList.Count - 1; i >= 0; i--)
            {
                var item = variable.ValuesList[i];
                if (item == null)
                {
                    variable.ValuesList.RemoveAt(i);
                    hasDuplicates = true;
                    continue;
                }

                var originalValue = item.Text ?? string.Empty;
                var trimmedValue = originalValue.Trim();
                item.Text = trimmedValue;
                if (!seenValues.Add(trimmedValue))
                {
                    variable.ValuesList.RemoveAt(i);
                    hasDuplicates = true;
                }
                else if (!string.Equals(originalValue, trimmedValue, StringComparison.Ordinal))
                {
                    hasValueChanges = true;
                }
            }

            if (hasDuplicates || hasValueChanges)
            {
                RefreshEditVariableDialogValueTextFromList(variable);
            }
        }

        private void InsertListEntryBeforeButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as MenuFlyoutItem)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            if (variable?.ValuesList == null)
            {
                return;
            }

            var index = variable.ValuesList.IndexOf(listItem);
            if (index < 0)
            {
                return;
            }

            variable.ValuesList.Insert(index, new Variable.ValuesListItem { Text = string.Empty });

            RefreshEditVariableDialogValueTextFromList(variable);
        }

        private void InsertListEntryAfterButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as MenuFlyoutItem)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            if (variable?.ValuesList == null)
            {
                return;
            }

            var index = variable.ValuesList.IndexOf(listItem);
            if (index < 0)
            {
                return;
            }

            variable.ValuesList.Insert(index + 1, new Variable.ValuesListItem { Text = string.Empty });

            RefreshEditVariableDialogValueTextFromList(variable);
        }

        private void EditVariableValuesListTextBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null)
            {
                return;
            }

            var listItem = textBox.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            if (listItem.Text == textBox.Text)
            {
                return;
            }

            listItem.Text = textBox.Text;
            var variable = EditVariableDialog.DataContext as Variable;
            if (variable?.ValuesList == null)
            {
                return;
            }

            RefreshEditVariableDialogValueTextFromList(variable);
        }

        private void RefreshEditVariableDialogValueTextFromList(Variable variable)
        {
            if (variable?.ValuesList == null)
            {
                return;
            }

            var newValues = string.Join(";", variable.ValuesList.Select(x => x?.Text).ToArray());
            EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged;
            EditVariableDialogValueTxtBox.Text = newValues;
            EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged;
        }

        private void InvalidStateInfoBar_CloseButtonClick(InfoBar sender, object args)
        {
            ViewModel.EnvironmentState = EnvironmentState.Unchanged;
        }

        private void AddVariableDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ResetAddVariableInputs();
        }
    }
}

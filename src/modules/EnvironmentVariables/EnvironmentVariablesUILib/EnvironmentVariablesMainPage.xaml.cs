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
using EnvironmentVariablesUILib.Helpers;
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
            if (EditVariableDialog == null || variable == null)
            {
                return;
            }

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
            if (btn == null)
            {
                return;
            }

            var variable = btn.CommandParameter as Variable;
            if (variable == null)
            {
                return;
            }

            var variablesSet = ResolveVariableSetFromVariable(variable);
            await ShowEditDialogAsync(variable, variablesSet);
        }

        private VariablesSet ResolveVariableSetFromVariable(Variable variable)
        {
            if (variable == null || ViewModel == null)
            {
                return null;
            }

            if (variable.ParentType == VariablesSetType.User)
            {
                return ViewModel.UserDefaultSet;
            }

            if (variable.ParentType == VariablesSetType.System)
            {
                return ViewModel.SystemDefaultSet;
            }

            if (variable.ParentType == VariablesSetType.Profile)
            {
                var exactMatch = ViewModel.Profiles?
                    .FirstOrDefault(profile => profile?.Variables != null && profile.Variables.Contains(variable));
                if (exactMatch != null)
                {
                    return exactMatch;
                }

                var normalizedName = (variable.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    LoggerInstance.Logger.LogError("Unable to resolve profile owner for invalid edited variable name.");
                    return null;
                }

                var matchingProfiles = ViewModel.Profiles?
                    .Where(profile => profile?.Variables != null &&
                        profile.Variables.Any(x => x != null &&
                            string.Equals((x.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase) &&
                            EnvironmentVariablesHelper.IsEquivalentVariableValue(x.Values, variable.Values) &&
                            x.ParentType == VariablesSetType.Profile))
                    .ToList();

                if (matchingProfiles == null || matchingProfiles.Count != 1)
                {
                    LoggerInstance.Logger.LogError("Unable to uniquely resolve profile owner for edited variable.");
                    return null;
                }

                return matchingProfiles[0];
            }

            return null;
        }

        private void EditVariable(RelayCommandParameter param)
        {
            if (param == null || ViewModel == null || EditVariableDialog == null)
            {
                return;
            }

            var variableSet = param.Set as ProfileVariablesSet;
            var original = param.Variable;
            var edited = EditVariableDialog.DataContext as Variable;
            if (original == null || edited == null)
            {
                return;
            }
            ViewModel.EditVariable(original, edited, variableSet);
        }

        private async Task AddProfileAsync()
        {
            if (AddProfileDialog == null || SwitchViewsSegmentedView == null)
            {
                return;
            }

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
            if (AddProfileDialog == null || ViewModel == null)
            {
                return;
            }

            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (profile == null)
            {
                return;
            }

            ViewModel.AddProfile(profile);
        }

        private void UpdateProfile()
        {
            if (AddProfileDialog == null || ViewModel == null)
            {
                return;
            }

            var updatedProfile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (updatedProfile == null)
            {
                return;
            }

            ViewModel.UpdateProfile(updatedProfile);
        }

        private async void RemoveProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            var button = sender as MenuFlyoutItem;
            if (button == null)
            {
                return;
            }

            var profile = button.CommandParameter as ProfileVariablesSet;
            if (profile == null)
            {
                return;
            }

            if (RootPage == null)
            {
                return;
            }

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

        private void AddVariable()
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (AddVariableDialog == null || AddProfileDialog == null || profile == null || profile.Variables == null || AddNewVariableName == null || AddNewVariableValue == null || ExistingVariablesListView == null || AddVariableSwitchPresenter == null)
            {
                ResetAddVariableInputs();
                AddVariableDialog?.Hide();
                return;
            }

            var activePanel = AddVariableSwitchPresenter.Value as string;
            if (activePanel == "NewVariable")
            {
                var newVariableName = AddNewVariableName?.Text?.Trim();
                var newVariable = new Variable(newVariableName, AddNewVariableValue?.Text, VariablesSetType.Profile);
                if (!newVariable.Valid)
                {
                    return;
                }

                if (newVariableName.Length >= 255 || profile.Variables.Any(x => string.Equals((x?.Name ?? string.Empty).Trim(), newVariableName, StringComparison.OrdinalIgnoreCase)))
                {
                    AddVariableDialog.IsPrimaryButtonEnabled = false;
                    return;
                }

                profile.Variables.Add(newVariable);
            }
            else
            {
                foreach (Variable variable in ExistingVariablesListView.SelectedItems)
                {
                    if (variable == null || string.IsNullOrWhiteSpace(variable.Name))
                    {
                        continue;
                    }

                    if (!profile.Variables.Any(x => AreEquivalentVariables(x, variable)))
                    {
                        var clone = variable.Clone(true);
                        profile.Variables.Add(clone);
                    }
                }
            }

            ResetAddVariableInputs();
            AddVariableDialog.Hide();
        }

        private void CancelAddVariable()
        {
            if (AddVariableDialog == null)
            {
                return;
            }

            ResetAddVariableInputs();
            AddVariableDialog.Hide();
        }

        private void ClearAddVariableSearchFilter()
        {
            if (ViewModel == null)
            {
                return;
            }

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
            if (ViewModel == null)
            {
                return;
            }

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
            if (AddNewVariableName != null)
            {
                AddNewVariableName.Text = string.Empty;
            }

            if (AddNewVariableValue != null)
            {
                AddNewVariableValue.Text = string.Empty;
            }

            if (AddVariableDialog != null)
            {
                AddVariableDialog.IsPrimaryButtonEnabled = false;
            }

            ClearAddVariableSearchFilter();

            if (ExistingVariablesListView != null)
            {
                ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
                ExistingVariablesListView.SelectedItems.Clear();
                ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
            }
        }

        private void AddDefaultVariable(DefaultVariablesSet set)
        {
            if (AddDefaultVariableDialog == null || ViewModel == null)
            {
                return;
            }

            if (set == null)
            {
                return;
            }

            var variable = AddDefaultVariableDialog.DataContext as Variable;
            if (variable == null)
            {
                return;
            }
            var type = set.Type;

            ViewModel.AddDefaultVariable(variable, type);
        }

        private async void Delete_Variable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            MenuFlyoutItem selectedItem = sender as MenuFlyoutItem;
            if (selectedItem == null || RootPage == null)
            {
                return;
            }

            var variableSet = selectedItem.DataContext as ProfileVariablesSet;
            var variable = selectedItem.CommandParameter as Variable;
            if (variable == null)
            {
                return;
            }

            if (variable.ParentType == VariablesSetType.Profile)
            {
                variableSet = ResolveVariableSetFromVariable(variable) as ProfileVariablesSet;
                if (variableSet == null)
                {
                    return;
                }
            }

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

        private void CopyVariableNameAndValue_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var selectedItem = sender as MenuFlyoutItem;
            var variable = selectedItem?.CommandParameter as Variable;
            if (variable == null)
            {
                return;
            }

            var value = variable.Values ?? string.Empty;
            CopyToClipboard($"{variable.Name}={value}");
        }

        private void CopyAppliedVariableNameAndValue_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as Button;
            var variable = button?.CommandParameter as Variable;
            if (variable == null)
            {
                return;
            }

            CopyToClipboard($"{variable.Name}={variable.Values ?? string.Empty}");
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
            if (AddProfileDialog == null || AddVariableDialog == null)
            {
                return;
            }

            TextBox nameTxtBox = sender as TextBox;
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (nameTxtBox == null || profile == null || profile.Variables == null)
            {
                AddVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            var normalizedName = nameTxtBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Contains('=') || normalizedName.Length >= 255 || profile.Variables.Any(x => string.Equals((x?.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                AddVariableDialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                AddVariableDialog.IsPrimaryButtonEnabled = true;
            }
        }

        private void ReloadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.LoadEnvironmentVariables();
            ViewModel.EnvironmentState = EnvironmentState.Unchanged;
        }

        private void ExistingVariablesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddVariableDialog == null || AddProfileDialog == null)
            {
                return;
            }

            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (profile == null)
            {
                return;
            }

            if (profile.Variables == null)
            {
                AddVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            if (e.AddedItems.Count > 0)
            {
                var list = sender as ListView;
                if (list == null)
                {
                    return;
                }

                var selectedVariables = list.SelectedItems.OfType<Variable>().ToList();
                var dedupedSelections = DeduplicateVariablesByEquivalence(selectedVariables);

                ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged;
                foreach (var item in selectedVariables.Except(dedupedSelections).ToList())
                {
                    list.SelectedItems.Remove(item);
                }

                ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged;
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

                    RemoveEquivalentProfileVariables(profile.Variables, removedVariable);
                }
            }

            AddVariableDialog.IsPrimaryButtonEnabled = false;
            foreach (Variable variable in ExistingVariablesListView.SelectedItems)
            {
                if (variable != null)
                {
                    if (!profile.Variables
                        .Any(x => AreEquivalentVariables(x, variable)))
                    {
                        AddVariableDialog.IsPrimaryButtonEnabled = true;
                        break;
                    }
                }
            }
        }

        private async void EditProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (SwitchViewsSegmentedView == null || AddProfileDialog == null)
            {
                return;
            }

            SwitchViewsSegmentedView.SelectedIndex = 0;

            var button = sender as MenuFlyoutItem;
            if (button == null)
            {
                return;
            }

            var profile = button.CommandParameter as ProfileVariablesSet;
            if (profile == null)
            {
                return;
            }

            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            AddProfileDialog.Title = resourceLoader.GetString("EditProfileDialog_Title");
            AddProfileDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
            AddProfileDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
            AddProfileDialog.PrimaryButtonCommand = UpdateProfileCommand;
            AddProfileDialog.DataContext = profile.Clone();
            await AddProfileDialog.ShowAsync();
        }

        private async void AddVariableBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (AddVariableDialog == null || SwitchViewsSegmentedView == null || AddProfileDialog == null)
            {
                return;
            }

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
            if (DefaultVariablesSearchBox == null || ExistingVariablesListView == null || AddVariableDialog == null || ViewModel == null)
            {
                return;
            }

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
            if (DefaultVariablesSearchBox == null || e == null)
            {
                return;
            }

            if (e.Key == VirtualKey.Escape && !string.IsNullOrEmpty(DefaultVariablesSearchBox.Text))
            {
                e.Handled = true;
                ClearAddVariableSearchFilter();
                DefaultVariablesSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private void ExistingVariablesListView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (AddProfileDialog == null)
            {
                return;
            }

            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (profile == null)
            {
                return;
            }

            if (profile.Variables == null || ExistingVariablesListView == null)
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

            return string.Equals((left.Name ?? string.Empty).Trim(), (right.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                && EnvironmentVariablesHelper.IsEquivalentVariableValue(left.Values, right.Values);
        }

        private static List<Variable> DeduplicateVariablesByEquivalence(IList<Variable> variables)
        {
            var deduped = new List<Variable>();
            foreach (var variable in variables)
            {
                if (deduped.Any(existing => AreEquivalentVariables(existing, variable)))
                {
                    continue;
                }

                deduped.Add(variable);
            }

            return deduped;
        }

        private static void RemoveEquivalentProfileVariables(IList<Variable> variables, Variable targetVariable)
        {
            if (variables == null || targetVariable == null)
            {
                return;
            }

            for (int i = variables.Count - 1; i >= 0; i--)
            {
                if (AreEquivalentVariables(variables[i], targetVariable))
                {
                    variables.RemoveAt(i);
                }
            }
        }

        private async Task ShowAddDefaultVariableDialogAsync(DefaultVariablesSet set)
        {
            if (set == null || AddDefaultVariableDialog == null)
            {
                return;
            }

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
            if (button == null)
            {
                return;
            }

            var defaultVariableSet = button.CommandParameter as DefaultVariablesSet;

            if (defaultVariableSet != null)
            {
                await ShowAddDefaultVariableDialogAsync(defaultVariableSet);
            }
        }

        private void EditVariableDialogNameTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EditVariableDialog == null || EditVariableDialogNameTxtBox == null || ViewModel == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            var param = EditVariableDialog.PrimaryButtonCommandParameter as RelayCommandParameter;
            var variableSet = param?.Set;
            var originalVariable = param?.Variable;
            if (variable == null)
            {
                EditVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            if (variableSet == null)
            {
                // default set
                variableSet = variable.ParentType == VariablesSetType.User ? ViewModel.UserDefaultSet : ViewModel.SystemDefaultSet;
            }

            if (variableSet != null)
            {
                var normalizedName = EditVariableDialogNameTxtBox.Text?.Trim();
                static bool IsCurrentVariable(Variable candidate, Variable reference)
                {
                    if (candidate == null || reference == null)
                    {
                        return false;
                    }

                    if (ReferenceEquals(candidate, reference))
                    {
                        return true;
                    }

                    return string.Equals((candidate.Name ?? string.Empty).Trim(), (reference.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                        && EnvironmentVariablesHelper.IsEquivalentVariableValue(candidate.Values, reference.Values)
                        && candidate.ParentType == reference.ParentType;
                }

                bool hasDuplicateName = variableSet.Variables != null &&
                    variableSet.Variables.Any(x => !IsCurrentVariable(x, originalVariable)
                        && string.Equals((x?.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(normalizedName) || variableSet.Variables == null || hasDuplicateName || !variable.Valid)
                {
                    EditVariableDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    EditVariableDialog.IsPrimaryButtonEnabled = true;
                }
            }
            else
            {
                EditVariableDialog.IsPrimaryButtonEnabled = false;
            }

            if (!variable.Validate())
            {
                EditVariableDialog.IsPrimaryButtonEnabled = false;
            }
        }

        private void AddDefaultVariableNameTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AddDefaultVariableDialog == null || ViewModel == null)
            {
                return;
            }

            TextBox nameTxtBox = sender as TextBox;
            var variable = AddDefaultVariableDialog.DataContext as Variable;
            if (nameTxtBox == null || variable == null)
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            var defaultSet = variable.ParentType == VariablesSetType.User ? ViewModel.UserDefaultSet : ViewModel.SystemDefaultSet;
            if (defaultSet == null)
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            var normalizedDefaultName = nameTxtBox.Text?.Trim();
            if (defaultSet.Variables == null || string.IsNullOrWhiteSpace(normalizedDefaultName) || defaultSet.Variables.Any(x => string.Equals((x?.Name ?? string.Empty).Trim(), normalizedDefaultName, StringComparison.OrdinalIgnoreCase)) || !variable.Validate())
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = true;
            }

            if (!variable.Validate())
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
            }
        }

        private void EditVariableDialogValueTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EditVariableDialog == null)
            {
                return;
            }

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
            if (EditVariableDialog == null)
            {
                return;
            }

            var menuFlyoutItem = sender as MenuFlyoutItem;
            if (menuFlyoutItem == null)
            {
                return;
            }

            var listItem = menuFlyoutItem.DataContext as Variable.ValuesListItem;
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
            if (EditVariableDialog == null)
            {
                return;
            }

            var menuFlyoutItem = sender as MenuFlyoutItem;
            if (menuFlyoutItem == null)
            {
                return;
            }

            var listItem = menuFlyoutItem.DataContext as Variable.ValuesListItem;
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
            if (EditVariableDialog == null)
            {
                return;
            }

            var menuFlyoutItem = sender as MenuFlyoutItem;
            if (menuFlyoutItem == null)
            {
                return;
            }

            var listItem = menuFlyoutItem.DataContext as Variable.ValuesListItem;
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
            if (EditVariableDialog == null)
            {
                return;
            }

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
            if (EditVariableDialog == null)
            {
                return;
            }

            var menuFlyoutItem = sender as MenuFlyoutItem;
            if (menuFlyoutItem == null)
            {
                return;
            }

            var listItem = menuFlyoutItem.DataContext as Variable.ValuesListItem;
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
            if (EditVariableDialog == null)
            {
                return;
            }

            var menuFlyoutItem = sender as MenuFlyoutItem;
            if (menuFlyoutItem == null)
            {
                return;
            }

            var listItem = menuFlyoutItem.DataContext as Variable.ValuesListItem;
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
            if (EditVariableDialog == null)
            {
                return;
            }

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

            if (EditVariableDialogValueTxtBox == null)
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
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.EnvironmentState = EnvironmentState.Unchanged;
        }

        private void AddVariableDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ResetAddVariableInputs();
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace EnvironmentVariablesUILib
{
    public sealed partial class EnvironmentVariablesMainPage : Page
    {
        private EnvironmentVariablesPageActions _actions;

        private EnvironmentVariablesPageActions Actions => _actions ??= new EnvironmentVariablesPageActions(
            () => ViewModel,
            () => EditVariableDialog.DataContext,
            () => AddProfileDialog.DataContext,
            () => AddDefaultVariableDialog.DataContext,
            AddProfileAsync,
            AddVariable,
            CancelAddVariable);

        public MainViewModel ViewModel { get; private set; }

        public ICommand EditCommand => Actions.EditCommand;

        public ICommand NewProfileCommand => Actions.NewProfileCommand;

        public ICommand AddProfileCommand => Actions.AddProfileCommand;

        public ICommand UpdateProfileCommand => Actions.UpdateProfileCommand;

        public ICommand AddVariableCommand => Actions.AddVariableCommand;

        public ICommand CancelAddVariableCommand => Actions.CancelAddVariableCommand;

        public ICommand AddDefaultVariableCommand => Actions.AddDefaultVariableCommand;

        public EnvironmentVariablesMainPage(MainViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            ViewModel.LoadEnvironmentVariables();
        }

        private async Task ShowEditDialogAsync(Variable variable, VariablesSet parentSet)
        {
            await EnvironmentVariablesPageActions.ShowVariableEditAsync(
                variable,
                () =>
                {
                    var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                    EditVariableDialog.Title = resourceLoader.GetString("EditVariableDialog_Title");
                    EditVariableDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
                    EditVariableDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
                    EditVariableDialog.PrimaryButtonCommand = EditCommand;
                    EditVariableDialog.PrimaryButtonCommandParameter = new EnvironmentVariablesPageActions.EditTarget(variable, parentSet);
                },
                clone => EditVariableDialog.DataContext = clone,
                UpdateEditVariableDialogPrimaryButtonState,
                async () => await EditVariableDialog.ShowAsync());
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

        private async Task AddProfileAsync()
        {
            await EnvironmentVariablesPageActions.ShowNewProfileAsync(
                () => SwitchViewsSegmentedView.SelectedIndex = 0,
                () =>
                {
                    var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                    AddProfileDialog.Title = resourceLoader.GetString("AddNewProfileDialog_Title");
                    AddProfileDialog.PrimaryButtonText = resourceLoader.GetString("AddBtn");
                    AddProfileDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
                    AddProfileDialog.PrimaryButtonCommand = AddProfileCommand;
                },
                profile => AddProfileDialog.DataContext = profile,
                async () => await AddProfileDialog.ShowAsync());
        }

        private async void RemoveProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as MenuFlyoutItem;
            var profile = button.CommandParameter as ProfileVariablesSet;

            await EnvironmentVariablesPageActions.ConfirmRemoveProfileAsync(() => ViewModel, profile, async (readTitle, primary) =>
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = RootPage.XamlRoot;
                dialog.Title = readTitle();
                dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
                dialog.CloseButtonText = resourceLoader.GetString("No");
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description"), TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords };
                dialog.PrimaryButtonClick += (s, args) =>
                {
                    primary();
                };

                var result = await dialog.ShowAsync();
            });
        }

        private void AddVariable()
        {
            EnvironmentVariablesPageActions.AddProfileVariable(
                () => AddProfileDialog.DataContext as ProfileVariablesSet,
                () => AddVariableSwitchPresenter.Value as string,
                () => AddNewVariableName.Text,
                () => AddNewVariableValue.Text,
                () => ExistingVariablesListView.SelectedItems,
                CancelAddVariable);
        }

        private void CancelAddVariable()
        {
            EnvironmentVariablesPageActions.CancelAddVariable(
                () => AddNewVariableName.Text = string.Empty,
                () => AddNewVariableValue.Text = string.Empty,
                () => ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged,
                () => ExistingVariablesListView.SelectedItems.Clear(),
                () => ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged,
                () => AddVariableFlyout.Hide());
        }

        private async void Delete_Variable_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            MenuFlyoutItem selectedItem = sender as MenuFlyoutItem;
            var variableSet = selectedItem.DataContext as ProfileVariablesSet;
            var variable = selectedItem.CommandParameter as Variable;

            await EnvironmentVariablesPageActions.ConfirmDeleteVariableAsync(() => ViewModel, variable, variableSet, async (readTitle, primary) =>
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = RootPage.XamlRoot;
                dialog.Title = readTitle();
                dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
                dialog.CloseButtonText = resourceLoader.GetString("No");
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Variable_Description"), TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords };
                dialog.PrimaryButtonClick += (s, args) =>
                {
                    primary();
                };
                var result = await dialog.ShowAsync();
            });
        }

        private void AddNewVariableName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateConfirmAddVariableButtonState();
        }

        private void AddNewVariableValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateConfirmAddVariableButtonState();
        }

        private void UpdateConfirmAddVariableButtonState()
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;
            if (profile == null)
            {
                ConfirmAddVariableBtn.IsEnabled = false;
                return;
            }

            if (AddVariableSwitchPresenter.Value as string == "NewVariable")
            {
                ConfirmAddVariableBtn.IsEnabled = VariableEditorState.CanAddProfileVariable(
                    profile, true, AddNewVariableName.Text, AddNewVariableValue.Text, Array.Empty<object>());
                return;
            }

            ConfirmAddVariableBtn.IsEnabled = VariableEditorState.CanAddProfileVariable(
                profile, false, null, null, ExistingVariablesListView.SelectedItems);
        }

        private void ReloadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            EnvironmentVariablesPageActions.Reload(ViewModel);
        }

        private void ExistingVariablesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;

            EnvironmentVariablesPageActions.SelectionChanged(
                profile,
                e.AddedItems.Count,
                () => (sender as ListView).SelectedItems.Cast<Variable>(),
                () => e.RemovedItems[0],
                () => e.RemovedItems.Count,
                () => ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged,
                duplicate => (sender as ListView).SelectedItems.Remove(duplicate),
                () => ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged,
                UpdateConfirmAddVariableButtonState);
        }

        private async void EditProfileBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await EnvironmentVariablesPageActions.ShowProfileEditAsync(
                () => SwitchViewsSegmentedView.SelectedIndex = 0,
                () =>
                {
                    var button = sender as MenuFlyoutItem;
                    return button.CommandParameter as ProfileVariablesSet;
                },
                () =>
                {
                    var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                    AddProfileDialog.Title = resourceLoader.GetString("EditProfileDialog_Title");
                    AddProfileDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
                    AddProfileDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
                    AddProfileDialog.PrimaryButtonCommand = UpdateProfileCommand;
                },
                profile => AddProfileDialog.DataContext = profile,
                async () => await AddProfileDialog.ShowAsync());
        }

        private void ExistingVariablesListView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var profile = AddProfileDialog.DataContext as ProfileVariablesSet;

            EnvironmentVariablesPageActions.LoadSelection(
                profile,
                ExistingVariablesListView.Items.Cast<Variable>(),
                ExistingVariablesListView.SelectedItems.Cast<Variable>(),
                () => ExistingVariablesListView.SelectionChanged -= ExistingVariablesListView_SelectionChanged,
                item => ExistingVariablesListView.SelectedItems.Add(item),
                () => ExistingVariablesListView.SelectionChanged += ExistingVariablesListView_SelectionChanged);
        }

        private async Task ShowAddDefaultVariableDialogAsync(DefaultVariablesSet set)
        {
            await EnvironmentVariablesPageActions.ShowDefaultAddAsync(
                set,
                () =>
                {
                    var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                    AddDefaultVariableDialog.Title = resourceLoader.GetString("AddVariable_Title");
                    AddDefaultVariableDialog.PrimaryButtonText = resourceLoader.GetString("SaveBtn");
                    AddDefaultVariableDialog.SecondaryButtonText = resourceLoader.GetString("CancelBtn");
                    AddDefaultVariableDialog.PrimaryButtonCommand = AddDefaultVariableCommand;
                    AddDefaultVariableDialog.PrimaryButtonCommandParameter = set;
                },
                variable => AddDefaultVariableDialog.DataContext = variable,
                UpdateAddDefaultVariableDialogButtonState,
                async () => await AddDefaultVariableDialog.ShowAsync());
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
            var txtBox = sender as TextBox;
            var variable = EditVariableDialog.DataContext as Variable;

            // Ensure Name is current regardless of binding timing.
            if (variable != null)
            {
                variable.Name = txtBox.Text;
            }

            UpdateEditVariableDialogPrimaryButtonState();
        }

        private void AddDefaultVariableNameTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txtBox = sender as TextBox;
            var variable = AddDefaultVariableDialog.DataContext as Variable;

            // Ensure Name is current regardless of binding timing.
            if (variable != null)
            {
                variable.Name = txtBox.Text;
            }

            UpdateAddDefaultVariableDialogButtonState();
        }

        private void AddDefaultVariableValueTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txtBox = sender as TextBox;
            var variable = AddDefaultVariableDialog.DataContext as Variable;

            // Ensure Values is current regardless of binding timing.
            if (variable != null)
            {
                variable.Values = txtBox.Text;
            }

            UpdateAddDefaultVariableDialogButtonState();
        }

        private void UpdateAddDefaultVariableDialogButtonState()
        {
            var variable = AddDefaultVariableDialog.DataContext as Variable;
            if (variable == null)
            {
                AddDefaultVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            var defaultSet = variable.ParentType == VariablesSetType.User ? ViewModel.UserDefaultSet : ViewModel.SystemDefaultSet;
            AddDefaultVariableDialog.IsPrimaryButtonEnabled = VariableEditorState.CanAddDefault(variable, defaultSet);
        }

        private void EditVariableDialogValueTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txtBox = sender as TextBox;
            EnvironmentVariablesPageActions.EditValueTextChanged(
                () => EditVariableDialog.DataContext as Variable,
                () => txtBox.Text,
                UpdateEditVariableDialogPrimaryButtonState);
        }

        private void UpdateEditVariableDialogPrimaryButtonState()
        {
            var variable = EditVariableDialog.DataContext as Variable;
            var param = EditVariableDialog.PrimaryButtonCommandParameter as EnvironmentVariablesPageActions.EditTarget;

            if (variable == null || param == null)
            {
                EditVariableDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            var variableSet = param.Set;
            if (variableSet == null)
            {
                variableSet = variable.ParentType == VariablesSetType.User ? ViewModel.UserDefaultSet : ViewModel.SystemDefaultSet;
            }

            EditVariableDialog.IsPrimaryButtonEnabled = VariableEditorState.CanSaveEdit(variable, param.Variable, variableSet);
        }

        private void ReorderButtonUp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = ((MenuFlyoutItem)sender).DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;

            var newValues = VariableEditorState.MoveUp(variable, listItem);
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

            var newValues = VariableEditorState.MoveDown(variable, listItem);
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
            var newValues = VariableEditorState.DeleteEntry(variable, listItem);
            EditVariableDialogValueTxtBox.Text = newValues;
        }

        private void RemoveDuplicatePathEntriesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var variable = EditVariableDialog.DataContext as Variable;
            if (variable == null)
            {
                return;
            }

            EditVariableDialogValueTxtBox.Text = EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(variable.Values);
        }

        private void InsertListEntryBeforeButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as MenuFlyoutItem)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            var newValues = VariableEditorState.InsertBefore(variable, listItem);
            EnvironmentVariablesPageActions.SetRawTextWithHandlerSuppressed(
                newValues,
                () => EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged,
                text => EditVariableDialogValueTxtBox.Text = text,
                () => EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged);
        }

        private void InsertListEntryAfterButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var listItem = (sender as MenuFlyoutItem)?.DataContext as Variable.ValuesListItem;
            if (listItem == null)
            {
                return;
            }

            var variable = EditVariableDialog.DataContext as Variable;
            var newValues = VariableEditorState.InsertAfter(variable, listItem);
            EnvironmentVariablesPageActions.SetRawTextWithHandlerSuppressed(
                newValues,
                () => EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged,
                text => EditVariableDialogValueTxtBox.Text = text,
                () => EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged);
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

            var variable = EditVariableDialog.DataContext as Variable;
            var newValues = VariableEditorState.CommitEntryText(variable, listItem, (sender as TextBox)?.Text);
            EnvironmentVariablesPageActions.SetRawTextWithHandlerSuppressed(
                newValues,
                () => EditVariableDialogValueTxtBox.TextChanged -= EditVariableDialogValueTxtBox_TextChanged,
                text => EditVariableDialogValueTxtBox.Text = text,
                () => EditVariableDialogValueTxtBox.TextChanged += EditVariableDialogValueTxtBox_TextChanged);
        }

        private void InvalidStateInfoBar_CloseButtonClick(InfoBar sender, object args)
        {
            EnvironmentVariablesPageActions.DismissWarning(ViewModel);
        }

        private void AddVariableFlyout_Closed(object sender, object e)
        {
            EnvironmentVariablesPageActions.FlyoutClosed(CancelAddVariable, () => ConfirmAddVariableBtn.IsEnabled = false);
        }
    }
}

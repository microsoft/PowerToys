// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptTemplatesPage : NavigablePage
{
    private bool _isCreating;
    private int _nextParameterId;

    private PowerScriptsViewModel ViewModel { get; set; }

    public ObservableCollection<PowerScriptTemplateParameter> Parameters { get; } = new();

    public PowerScriptTemplatesPage()
    {
        InitializeComponent();
        UpdateOptions();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel ??
            throw new ArgumentException("Creation navigation requires the PowerScripts view model.", nameof(e));
        TemplateBreadcrumb.ItemsSource = new[] { "PowerScripts", ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_CreateScriptTitle") };
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCreating)
        {
            NavigationService.GoBack();
        }
    }

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = _isCreating || NavigationService.GoBack();

    private void TemplateBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0 && !_isCreating)
        {
            NavigationService.GoBack();
        }
    }

    private void ProjectName_TextChanged(object sender, TextChangedEventArgs e) => UpdateCreateButton();

    private void UpdateCreateButton()
    {
        if (CreateButton is not null)
        {
            var hasOptions = RuntimeComboBox.SelectedItem is not null &&
                InputComboBox.SelectedItem is not null && OutputComboBox.SelectedItem is not null;
            var hasFileCounts = (InputComboBox.SelectedItem as ComboBoxItem)?.Tag as string != "files" ||
                (!double.IsNaN(MinimumFilesNumberBox.Value) && !double.IsNaN(MaximumFilesNumberBox.Value));
            CreateButton.IsEnabled = !_isCreating && !string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) &&
                hasOptions && hasFileCounts;
        }
    }

    private void Options_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOptions();

    private void FileCount_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateCreateButton();

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static string ParameterHeader(string name) =>
        string.IsNullOrWhiteSpace(name) ? ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateParameterHeader") : name;

    private void AddParameterButton_Click(object sender, RoutedEventArgs e) =>
        Parameters.Add(new PowerScriptTemplateParameter(++_nextParameterId));

    private void RemoveParameterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PowerScriptTemplateParameter parameter })
        {
            Parameters.Remove(parameter);
            AddParameterButton.Focus(FocusState.Programmatic);
        }
    }

    private void UpdateOptions()
    {
        if (FileInputOptions is null || PythonHelp is null)
        {
            return;
        }

        FileInputOptions.Visibility = (InputComboBox.SelectedItem as ComboBoxItem)?.Tag as string == "files" ? Visibility.Visible : Visibility.Collapsed;
        PythonHelp.Visibility = (RuntimeComboBox.SelectedItem as ComboBoxItem)?.Tag as string == "python" ? Visibility.Visible : Visibility.Collapsed;
        UpdateCreateButton();
    }

    private static string SelectedToken(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag as string ??
        throw new InvalidOperationException("Select a script runtime and input/output types.");

    private static int FileCount(NumberBox numberBox)
    {
        var value = numberBox.Value;
        if (!double.IsFinite(value) || value != Math.Truncate(value) || value < 0 || value > int.MaxValue)
        {
            throw new ArgumentException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateInvalidFileCount"));
        }

        return (int)value;
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreating)
        {
            return;
        }

        _isCreating = true;
        CreationForm.IsEnabled = false;
        TemplateStatus.IsOpen = false;
        string entry = null;
        try
        {
            var input = SelectedToken(InputComboBox);
            entry = await ViewModel.CreateTemplateAsync(
                ProjectNameTextBox.Text,
                DescriptionTextBox.Text,
                SelectedToken(RuntimeComboBox),
                input,
                SelectedToken(OutputComboBox),
                input == "files" ? ExtensionsTextBox.Text : null,
                input == "files" ? FileCount(MinimumFilesNumberBox) : 1,
                input == "files" ? FileCount(MaximumFilesNumberBox) : 0,
                new JsonArray(Parameters.Select(parameter => (JsonNode)parameter.ToTemplateParameter()).ToArray()));
            TemplateStatus.Severity = InfoBarSeverity.Success;
            TemplateStatus.Message = ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateCreated") +
                Environment.NewLine + Path.GetDirectoryName(entry);
            TemplateStatus.IsOpen = true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to create configured PowerScript.", ex);
            TemplateStatus.Severity = InfoBarSeverity.Error;
            TemplateStatus.Message = ex.Message;
            TemplateStatus.IsOpen = true;
        }
        finally
        {
            _isCreating = false;
            CreationForm.IsEnabled = true;
            UpdateCreateButton();
        }

        if (entry is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{entry}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.LogError("PowerScript created, but its folder could not be opened.", ex);
                TemplateStatus.Severity = InfoBarSeverity.Warning;
                TemplateStatus.Message += Environment.NewLine +
                    ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateFolderFailed") + " " + ex.Message;
            }
        }
    }
}

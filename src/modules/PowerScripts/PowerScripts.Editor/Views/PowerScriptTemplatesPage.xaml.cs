// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.Client;
using PowerScripts.Editor.Helpers;
using PowerScripts.UI.Helpers;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class PowerScriptTemplatesPage : Page
{
    private readonly Dictionary<string, string> _selectedCliActions = new(StringComparer.Ordinal);
    private bool _isCreating;
    private int _nextParameterId;
    private string _customIconPath;
    private bool _updatingGuidance;
    private bool _loadingCliContent;
    private bool _isActive;
    private bool _updatingCliSelection;

    private bool IsCliContent => (ContentKindComboBox?.SelectedItem as ComboBoxItem)?.Tag as string == "cli";

    private PowerScriptOperationTemplate SelectedCliContent => IsCliContent ? (CliContentComboBox?.SelectedItem as ComboBoxItem)?.Tag as PowerScriptOperationTemplate : null;

    private PowerScriptCliModule SelectedCliModule => (CliModuleComboBox?.SelectedItem as ComboBoxItem)?.Tag as PowerScriptCliModule;

    private PowerScriptsViewModel ViewModel { get; set; }

    internal bool IsCreating => _isCreating;

    public ObservableCollection<PowerScriptTemplateParameter> Parameters { get; } = new();

    public PowerScriptTemplatesPage()
    {
        InitializeComponent();
        UpdateOptions();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel
            ?? throw new ArgumentException("Creation navigation requires the PowerScripts view model.", nameof(e));
        _isActive = true;
        RuntimeComboBox.SelectedIndex = 0;
        ContentKindComboBox.SelectedIndex = 0;
        InputComboBox.SelectedIndex = 0;
        OutputComboBox.SelectedIndex = 1;
        IconGrid.SelectedIndex = 0;
        TemplateBreadcrumb.ItemsSource = new[] { "PowerScripts", ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_CreateScriptTitle") };
        UpdatePreview();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        CliModuleComboBox.IsDropDownOpen = false;
        CliContentComboBox.IsDropDownOpen = false;
        base.OnNavigatedFrom(e);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCreating)
        {
            EditorNavigation.GoBack(Frame);
        }
    }

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = _isCreating || EditorNavigation.GoBack(Frame);

    private void TemplateBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0 && !_isCreating)
        {
            EditorNavigation.GoBack(Frame);
        }
    }

    private void ProjectName_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCreateButton();
        UpdatePreview();
    }

    private void UpdateCreateButton()
    {
        if (CreateButton is not null)
        {
            var hasOptions = RuntimeComboBox.SelectedItem is not null &&
                InputComboBox.SelectedItem is not null && OutputComboBox.SelectedItem is not null;
            var hasFileCounts = (InputComboBox.SelectedItem as GridViewItem)?.Tag as string != "files" ||
                (!double.IsNaN(MinimumFilesNumberBox.Value) && !double.IsNaN(MaximumFilesNumberBox.Value));
            CreateButton.IsEnabled = !_isCreating && (!IsCliContent || !_loadingCliContent) && !string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) &&
                hasOptions && hasFileCounts && ValidateCliArguments(out _);
        }
    }

    private void Options_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, IconGrid) && IconGrid.SelectedItem is not null)
        {
            _customIconPath = null;
        }

        if (_loadingCliContent || _updatingCliSelection)
        {
            return;
        }

        UpdateOptions();
    }

    private void Consumer_Changed(object sender, RoutedEventArgs e) => UpdateOptions();

    private async void Content_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (IsCliContent)
        {
            RuntimeComboBox.SelectedIndex = 0;
        }

        UpdateOptions();
        if (IsCliContent && CliModuleComboBox.Items.Count == 0 && !_loadingCliContent)
        {
            await LoadCliContentAsync();
        }
    }

    private void RestrictedOperation_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCreating && !Frame.Navigate(typeof(PowerToysOperationsPage), ViewModel))
        {
            TemplateStatus.Severity = InfoBarSeverity.Error;
            TemplateStatus.Message = PowerScriptsViewModel.GetResourceString("PowerScripts_OperationNavigationFailed");
            TemplateStatus.IsOpen = true;
        }
    }

    private void CliModule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingCliContent && !_updatingCliSelection)
        {
            UpdateCliActions();
            UpdateOptions();
        }
    }

    private bool AllowsCliTemplate(PowerScriptOperationTemplate template) =>
        PossibleCliInputs(template).Any(input => PowerScriptConsumerGuidance.Allows(SelectedConsumers, input, template.Output));

    private void UpdateCliActions()
    {
        _updatingCliSelection = true;
        try
        {
            if (SelectedCliContent is { } previous)
            {
                _selectedCliActions[previous.ModuleId] = previous.Id;
            }

            CliContentComboBox.Items.Clear();
            if (SelectedCliModule is not { } module)
            {
                return;
            }

            foreach (var template in module.Templates)
            {
                var item = new ComboBoxItem { Content = template.Name, Tag = template, IsEnabled = AllowsCliTemplate(template) };
                AutomationProperties.SetAutomationId(item, "PowerScriptsTemplateCliContent_" + template.Id);
                CliContentComboBox.Items.Add(item);
            }

            _selectedCliActions.TryGetValue(module.Id, out var preferred);
            var items = CliContentComboBox.Items.Cast<ComboBoxItem>().ToArray();
            CliContentComboBox.SelectedItem = items.FirstOrDefault(item =>
                item.IsEnabled && ((PowerScriptOperationTemplate)item.Tag).Id == preferred) ?? items.FirstOrDefault(item => item.IsEnabled);
        }
        finally
        {
            _updatingCliSelection = false;
        }
    }

    private async void CliCatalogueRetry_Click(object sender, RoutedEventArgs e) => await LoadCliContentAsync();

    private async Task LoadCliContentAsync()
    {
        if (_loadingCliContent || !_isActive)
        {
            return;
        }

        _loadingCliContent = true;
        CliCatalogueStatus.Severity = InfoBarSeverity.Informational;
        CliCatalogueStatus.Message = PowerScriptsViewModel.GetResourceString("PowerScripts_CliCatalogueLoading");
        CliCatalogueStatus.IsOpen = true;
        CliCatalogueRetry.Visibility = Visibility.Collapsed;
        CliModuleComboBox.PlaceholderText = CliCatalogueStatus.Message;
        UpdateOptions();
        try
        {
            var templates = await ViewModel.LoadCliContentTemplatesAsync();
            if (!_isActive)
            {
                return;
            }

            if (templates.Count == 0)
            {
                throw new InvalidDataException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateCliEmpty"));
            }

            var modules = PowerScriptCliModule.FromTemplates(templates);
            CliModuleComboBox.Items.Clear();
            foreach (var module in modules)
            {
                var item = new ComboBoxItem { Content = module.Name, Tag = module, IsEnabled = module.Templates.Any(AllowsCliTemplate) };
                AutomationProperties.SetAutomationId(item, "PowerScriptsTemplateCliModule_" + module.Id);
                CliModuleComboBox.Items.Add(item);
            }

            CliModuleComboBox.SelectedItem = CliModuleComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.IsEnabled);
            UpdateCliActions();
            CliCatalogueStatus.IsOpen = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to load PowerShell module CLI content.", ex);
            if (_isActive)
            {
                CliContentComboBox.Items.Clear();
                CliModuleComboBox.Items.Clear();
                CliCatalogueStatus.Severity = InfoBarSeverity.Error;
                CliCatalogueStatus.Message = ex.Message;
                CliCatalogueStatus.IsOpen = true;
                CliCatalogueRetry.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _loadingCliContent = false;
            if (_isActive)
            {
                CliModuleComboBox.PlaceholderText = PowerScriptsViewModel.GetResourceString("PowerScripts_TemplateCliModule.PlaceholderText");
                UpdateOptions();
            }
        }
    }

    private void UpdateConsumerGuidance()
    {
        if (_updatingGuidance || ConsumerChoices is null || InputComboBox is null || OutputComboBox is null)
        {
            return;
        }

        _updatingGuidance = true;
        try
        {
            var choices = ConsumerChoices.Children.OfType<CheckBox>().ToArray();
            var selected = SelectedConsumers;
            var cli = SelectedCliContent;
            foreach (var choice in choices)
            {
                choice.IsEnabled = choice.IsChecked == true ||
                    (cli is null ? PowerScriptConsumerGuidance.CanCombine(selected.Append((string)choice.Tag)) :
                        PowerScriptConsumerGuidance.Allows(selected.Append((string)choice.Tag), CliInput, cli.Output));
            }

            if (CliContentComboBox is not null)
            {
                foreach (var item in CliModuleComboBox.Items.Cast<ComboBoxItem>())
                {
                    item.IsEnabled = ((PowerScriptCliModule)item.Tag).Templates.Any(AllowsCliTemplate);
                }

                foreach (var item in CliContentComboBox.Items.Cast<ComboBoxItem>())
                {
                    var template = (PowerScriptOperationTemplate)item.Tag;
                    item.IsEnabled = PossibleCliInputs(template).Any(input => PowerScriptConsumerGuidance.Allows(selected, input, template.Output));
                }
            }

            foreach (var argument in CliArguments)
            {
                argument.CanUseInput = argument.SourceIndex == 1 ||
                    (CliArguments.Where(other => other != argument && other.SourceIndex == 1).All(other => other.InputType == argument.InputType) &&
                        PowerScriptConsumerGuidance.Allows(selected, argument.InputType, cli.Output));
            }

            var inputs = InputComboBox.Items.Cast<GridViewItem>().ToArray();
            var outputs = OutputComboBox.Items.Cast<GridViewItem>().ToArray();
            foreach (var item in inputs)
            {
                item.IsEnabled = (cli is null || (string)item.Tag == CliInput) &&
                    outputs.Any(output => PowerScriptConsumerGuidance.Allows(selected, (string)item.Tag, (string)output.Tag));
            }

            if (InputComboBox.SelectedItem is not GridViewItem { IsEnabled: true })
            {
                InputComboBox.SelectedItem = inputs.FirstOrDefault(item => item.IsEnabled);
            }

            var input = (InputComboBox.SelectedItem as GridViewItem)?.Tag as string;
            foreach (var item in outputs)
            {
                item.IsEnabled = input is not null && (cli is null || (string)item.Tag == cli.Output) &&
                    PowerScriptConsumerGuidance.Allows(selected, input, (string)item.Tag);
            }

            if (OutputComboBox.SelectedItem is not GridViewItem { IsEnabled: true })
            {
                OutputComboBox.SelectedItem = outputs.FirstOrDefault(item => item.IsEnabled);
            }
        }
        finally
        {
            _updatingGuidance = false;
        }
    }

    private async void CustomIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreating)
        {
            return;
        }

        _isCreating = true;
        App.MainWindow.UpdateBackButton();
        UpdateCreateButton();
        CustomIconButton.IsEnabled = false;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
            picker.FileTypeFilter.Add(".ico");
            var result = await picker.PickSingleFileAsync();
            if (result is null)
            {
                return;
            }

            using var stream = File.OpenRead(result.Path);
            if (stream.Length > 1024 * 1024 || !string.Equals(Path.GetExtension(result.Path), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateCustomIconInvalid"));
            }

            var image = new BitmapImage();
            await image.SetSourceAsync(stream.AsRandomAccessStream());
            _customIconPath = result.Path;
            IconGrid.SelectedItem = null;
            TemplateStatus.IsOpen = false;
            UpdateOptions();
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to choose a custom PowerScript icon.", ex);
            TemplateStatus.Severity = InfoBarSeverity.Error;
            TemplateStatus.Message = ex.Message;
            TemplateStatus.IsOpen = true;
        }
        finally
        {
            _isCreating = false;
            CustomIconButton.IsEnabled = true;
            App.MainWindow.UpdateBackButton();
            UpdateCreateButton();
        }
    }

    private void FileCount_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateCreateButton();

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility InvertBoolToVisibility(bool value) => BoolToVisibility(!value);

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
        UpdateCliArguments();
        UpdateConsumerGuidance();
        if (FileInputOptions is null || PythonHelp is null)
        {
            return;
        }

        FileInputOptions.Visibility = (InputComboBox.SelectedItem as GridViewItem)?.Tag as string == "files" ? Visibility.Visible : Visibility.Collapsed;
        if (ContentKindComboBox is not null && RuntimeComboBox is not null)
        {
            RuntimeComboBox.IsEnabled = !IsCliContent;
            CliContentOptions.Visibility = BoolToVisibility(IsCliContent);
            CliModuleComboBox.IsEnabled = !_loadingCliContent && CliModuleComboBox.Items.Count > 0;
            CliContentComboBox.IsEnabled = !_loadingCliContent && CliContentComboBox.Items.Count > 0;
            var cli = SelectedCliContent;
            CliDescription.Text = cli?.Description ?? string.Empty;
            ValidateCliArguments(out var cliError);
            CliBindingValidation.Text = cliError ?? string.Empty;
            CliBindingValidation.Visibility = BoolToVisibility(cliError is not null);
        }

        PythonHelp.Visibility = (RuntimeComboBox.SelectedItem as ComboBoxItem)?.Tag as string == "python" ? Visibility.Visible : Visibility.Collapsed;
        if (CustomIconName is not null)
        {
            CustomIconName.Text = _customIconPath is null ? string.Empty : Path.GetFileName(_customIconPath);
            CustomIconName.Visibility = BoolToVisibility(_customIconPath is not null);
        }

        UpdateCreateButton();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewRow is null || RuntimeComboBox?.SelectedItem is not ComboBoxItem runtime)
        {
            return;
        }

        var preview = new PowerScriptListItem
        {
            Name = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text)
                ? ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_CreationUntitled") : ProjectNameTextBox.Text,
            Runtime = (string)runtime.Content,
            IconPath = _customIconPath ?? (IconGrid?.SelectedItem as PowerScriptTemplateIcon)?.IconPath,
            Io = new PowerScriptIo
            {
                Input = (InputComboBox.SelectedItem as GridViewItem)?.Tag as string ?? "none",
                Output = (OutputComboBox.SelectedItem as GridViewItem)?.Tag as string ?? "text",
            },
        };
        PreviewName.Text = preview.Name;
        PreviewSummary.Text = preview.CatalogueSummary;
        AutomationProperties.SetName(PreviewRow, preview.Name);
        PreviewIconGlyph.Glyph = preview.DefaultIconGlyph;
        PreviewIconImage.Source = preview.HasIcon ? new BitmapImage(new Uri(preview.IconSourcePath)) : null;
        PreviewIconImage.Visibility = preview.HasIcon ? Visibility.Visible : Visibility.Collapsed;
        PreviewIconGlyph.Visibility = preview.HasIcon ? Visibility.Collapsed : Visibility.Visible;

        var entry = (string)runtime.Tag == "python" ? "script.py" : "script.ps1";
        PreviewFiles.Text = entry + Environment.NewLine + entry + ".tool.json" + Environment.NewLine + "README.md";
        if (preview.HasIcon)
        {
            PreviewFiles.Text += Environment.NewLine + "icon.ico";
        }
    }

    private void PreviewIcon_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        PreviewIconImage.Visibility = Visibility.Collapsed;
        PreviewIconGlyph.Visibility = Visibility.Visible;
    }

    private static string SelectedToken(Control control)
    {
        var selected = control switch
        {
            ComboBox comboBox => comboBox.SelectedItem,
            ListViewBase list => list.SelectedItem,
            _ => null,
        };
        return (selected as FrameworkElement)?.Tag as string ??
            throw new InvalidOperationException("Select a script runtime and input/output types.");
    }

    private static int FileCount(NumberBox numberBox)
    {
        var value = numberBox.Value;
        if (!double.IsFinite(value) || value != Math.Truncate(value) || value < 0 || value > int.MaxValue)
        {
            throw new ArgumentException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateInvalidFileCount"));
        }

        return (int)value;
    }

    private void ChoiceGrid_Loaded(object sender, RoutedEventArgs e) => ResizeChoices((GridView)sender);

    private void IconGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is PowerScriptTemplateIcon icon)
        {
            AutomationProperties.SetAutomationId(args.ItemContainer, icon.AutomationId);
            AutomationProperties.SetName(args.ItemContainer, icon.Name);
        }
    }

    private void ChoiceGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ResizeChoices((GridView)sender);

    private static void ResizeChoices(GridView grid)
    {
        if (grid.ItemsPanelRoot is ItemsWrapGrid panel)
        {
            var columns = Math.Clamp((int)(grid.ActualWidth / 160), 1, 3);
            panel.ItemWidth = Math.Max(120, (grid.ActualWidth - 8) / columns);
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreating)
        {
            return;
        }

        _isCreating = true;
        App.MainWindow.UpdateBackButton();
        CreateButton.IsEnabled = false;
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
                new JsonArray(Parameters.Select(parameter => (JsonNode)parameter.ToTemplateParameter()).ToArray()),
                IconGrid.SelectedItem is PowerScriptTemplateIcon { HasIcon: true } icon ? icon.Id : null,
                _customIconPath,
                cliContent: SelectedCliContent?.Id,
                cliBindings: CreateCliBindings());
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
            App.MainWindow.UpdateBackButton();
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

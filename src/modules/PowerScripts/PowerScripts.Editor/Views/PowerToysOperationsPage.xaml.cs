// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using PowerScripts.Editor.Helpers;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Navigation cancels the request; its asynchronous finally block disposes the owned token source.")]
public sealed partial class PowerToysOperationsPage : Page
{
    private readonly Dictionary<string, PowerScriptOperationDraft> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedOperationIds = new(StringComparer.OrdinalIgnoreCase);
    private PowerScriptsViewModel _viewModel;
    private PowerScriptOperationDraft _draft;
    private CancellationTokenSource _testCancellation;
    private bool _updatingSelection = true;
    private bool _isLoading;
    private bool _isCreating;
    private bool _isTesting;
    private bool _isBrowsing;
    private bool _isActive;
    private bool _showValidationErrors;
    private int _pageGeneration;
    private int _draftRevision;

    public PowerToysOperationsPage()
    {
        InitializeComponent();
        _updatingSelection = false;
        UpdateState();
    }

    private PowerScriptOperationTemplate SelectedTemplate => OperationComboBox.SelectedItem as PowerScriptOperationTemplate;

    private PowerScriptOperationModule SelectedModule => ModuleComboBox.SelectedItem as PowerScriptOperationModule;

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as PowerScriptsViewModel
            ?? throw new ArgumentException("Operation creation requires the PowerScripts view model.", nameof(e));
        _isActive = true;
        OperationsBreadcrumb.ItemsSource = new[] { "PowerScripts", Resource("PowerScripts_OperationsTitle") };
        await LoadOperationsAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        ++_pageGeneration;
        _testCancellation?.Cancel();
        TestResultFlyout.Hide();
        DetachParameters();
        base.OnNavigatedFrom(e);
    }

    private static string Resource(string key) => PowerScriptsViewModel.GetResourceString(key);

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCreating)
        {
            EditorNavigation.GoBack(Frame);
        }
    }

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = _isCreating || EditorNavigation.GoBack(Frame);

    private void OperationsBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0 && !_isCreating)
        {
            EditorNavigation.GoBack(Frame);
        }
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e) => await LoadOperationsAsync();

    private async Task LoadOperationsAsync()
    {
        if (_isLoading || _isCreating || _isTesting || _viewModel is null)
        {
            return;
        }

        var generation = _pageGeneration;
        var selectedId = SelectedTemplate?.Id;
        var selectedModuleId = SelectedModule?.Id;
        _isLoading = true;
        CatalogueStatus.IsOpen = false;
        ModuleComboBox.PlaceholderText = Resource("PowerScripts_OperationsLoading");
        OperationComboBox.PlaceholderText = Resource("PowerScripts_OperationsLoading");
        UpdateState();
        try
        {
            var templates = await _viewModel.LoadOperationTemplatesAsync();
            if (!_isActive || generation != _pageGeneration)
            {
                return;
            }

            var modules = PowerScriptOperationModule.FromTemplates(templates);
            _updatingSelection = true;
            ModuleComboBox.ItemsSource = modules;
            ModuleComboBox.SelectedItem = selectedModuleId is null
                ? (modules.Count > 0 ? modules[0] : null)
                : modules.FirstOrDefault(module => string.Equals(module.Id, selectedModuleId, StringComparison.OrdinalIgnoreCase));
            UpdateOperationChoices(selectedId, requirePreferred: selectedId is not null);
            _updatingSelection = false;
            if (templates.Count == 0)
            {
                CatalogueStatus.Message = Resource("PowerScripts_OperationsEmpty");
                CatalogueStatus.IsOpen = true;
            }
            else if (selectedId is not null && SelectedTemplate is null)
            {
                CatalogueStatus.Message = Resource("PowerScripts_OperationTemplateUnavailable");
                CatalogueStatus.IsOpen = true;
            }

            SelectOperation();
        }
        catch (Exception ex)
        {
            if (_isActive && generation == _pageGeneration)
            {
                Logger.LogError("Unable to load PowerToys operation templates.", ex);
                CatalogueStatus.Message = ex.Message;
                CatalogueStatus.IsOpen = true;
            }
        }
        finally
        {
            _updatingSelection = false;
            _isLoading = false;
            if (_isActive && generation == _pageGeneration)
            {
                ModuleComboBox.PlaceholderText = Resource("PowerScripts_OperationsModuleSelector.PlaceholderText");
                OperationComboBox.PlaceholderText = Resource("PowerScripts_OperationsSelector.PlaceholderText");
                UpdateState();
            }
        }
    }

    private void Module_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        _updatingSelection = true;
        try
        {
            UpdateOperationChoices();
        }
        finally
        {
            _updatingSelection = false;
        }

        SelectOperation();
    }

    private void UpdateOperationChoices(string preferredId = null, bool requirePreferred = false)
    {
        var module = SelectedModule;
        OperationComboBox.ItemsSource = module?.Operations;
        if (preferredId is null && module is not null)
        {
            _selectedOperationIds.TryGetValue(module.Id, out preferredId);
        }

        var preferred = module?.Operations.FirstOrDefault(operation =>
            string.Equals(operation.Id, preferredId, StringComparison.OrdinalIgnoreCase));
        var first = module is not null && module.Operations.Count > 0 ? module.Operations[0] : null;
        OperationComboBox.SelectedItem = preferred ?? (requirePreferred ? null : first);
    }

    private void Operation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingSelection)
        {
            SelectOperation();
        }
    }

    private void SelectOperation()
    {
        DetachParameters();
        _draft = null;
        _updatingSelection = true;
        _showValidationErrors = false;
        CreationStatus.IsOpen = false;
        ResetTestResult();
        try
        {
            if (SelectedTemplate is { } template)
            {
                _selectedOperationIds[template.ModuleId] = template.Id;
                if (!_drafts.TryGetValue(template.Id, out _draft))
                {
                    _draft = new PowerScriptOperationDraft(template);
                    _drafts.Add(template.Id, _draft);
                }

                foreach (var parameter in _draft.Parameters)
                {
                    parameter.PropertyChanged += Parameter_PropertyChanged;
                }

                ProjectNameTextBox.Text = _draft.ProjectName;
                OperationDescription.Text = template.Description;
            }
            else
            {
                ProjectNameTextBox.Text = string.Empty;
                OperationDescription.Text = string.Empty;
            }

            ParametersRepeater.ItemsSource = _draft?.Parameters;
            ++_draftRevision;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to render a PowerToys operation template.", ex);
            CatalogueStatus.Message = ex.Message;
            CatalogueStatus.IsOpen = true;
            _draft = null;
            ParametersRepeater.ItemsSource = null;
        }
        finally
        {
            _updatingSelection = false;
            UpdateState();
        }
    }

    private void DetachParameters()
    {
        if (_draft is not null)
        {
            foreach (var parameter in _draft.Parameters)
            {
                parameter.PropertyChanged -= Parameter_PropertyChanged;
            }
        }
    }

    private void ProjectName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingSelection && _draft is not null)
        {
            _draft.ProjectName = ProjectNameTextBox.Text;
            UpdateState();
        }
    }

    private void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        ++_draftRevision;
        _showValidationErrors = true;
        ResetTestResult();
        UpdateState();
    }

    private void UpdateState()
    {
        if (CreateButton is null)
        {
            return;
        }

        var canEdit = !_isLoading && !_isCreating && !_isTesting;
        var template = SelectedTemplate;
        ProjectNameTextBox.IsEnabled = canEdit && _draft is not null;
        ModuleComboBox.IsEnabled = canEdit;
        OperationComboBox.IsEnabled = canEdit && SelectedModule is not null;
        ParametersForm.IsEnabled = canEdit;
        OperationDescription.Visibility = BoolToVisibility(!string.IsNullOrWhiteSpace(template?.Description));
        ParametersForm.Visibility = BoolToVisibility(_draft is not null);
        NoParametersText.Visibility = BoolToVisibility(_draft?.Parameters.Count == 0);
        var validConfiguration = _draft is not null && _draft.TryBuildConfiguration(out _, out _, out _);
        CreateButton.IsEnabled = canEdit && validConfiguration && !string.IsNullOrWhiteSpace(ProjectNameTextBox.Text);
        var testDisabledReason = GetTestRunDisabledReason();
        TestRunButton.IsEnabled = testDisabledReason is null;
        TestRunButton.IsHitTestVisible = TestRunButton.IsEnabled;
        TestRunLabel.Text = Resource(_isTesting ? "PowerScripts_OperationsCancelTest" : "PowerScripts_OperationsTest.Text");
        AutomationProperties.SetName(TestRunButton, TestRunLabel.Text);
        AutomationProperties.SetHelpText(TestRunButton, testDisabledReason ?? string.Empty);
        AutomationProperties.SetHelpText(TestRunButtonHost, testDisabledReason ?? string.Empty);
        TestRunIcon.Glyph = _isTesting ? "\uE71A" : "\uE768";
        TestRunDisabledReasonText.Text = testDisabledReason ?? string.Empty;
        TestRunUnavailableToolTip.IsEnabled = testDisabledReason is not null;
        if (!_isTesting && _draft is not null)
        {
            _draft.TryBuildConfiguration(out _, out _, out var error);
            ValidationMessage.Text = error ?? string.Empty;
            ValidationMessage.Visibility = BoolToVisibility(_showValidationErrors && error is not null);
        }
    }

    private string GetTestRunDisabledReason()
    {
        if (_isTesting)
        {
            return null;
        }

        if (_isLoading)
        {
            return Resource("PowerScripts_OperationsLoading");
        }

        if (_isCreating)
        {
            return Resource("PowerScripts_OperationsTestWhileSaving");
        }

        if (_draft is null || SelectedTemplate is null)
        {
            return Resource("PowerScripts_OperationsTestChooseOperation");
        }

        if (!string.IsNullOrWhiteSpace(SelectedTemplate.UnavailableReason))
        {
            return SelectedTemplate.UnavailableReason;
        }

        if (!_draft.TryBuildConfiguration(out _, out _, out var error) || !_draft.TryGetTestValues(out _, out error))
        {
            return error ?? Resource("PowerScripts_OperationsTestCompleteParameters");
        }

        return null;
    }

    private void ResetTestResult()
    {
        TestResultFlyout.Hide();
        TestStatus.Text = string.Empty;
        TestStatus.Visibility = Visibility.Collapsed;
        ValidationMessage.Text = string.Empty;
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private async void BrowseFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBrowsing || _isCreating || _isTesting ||
            sender is not Button { Tag: PowerScriptOperationParameter parameter } button || App.MainWindow is null)
        {
            return;
        }

        _isBrowsing = true;
        var generation = _pageGeneration;
        var revision = _draftRevision;
        button.IsEnabled = false;
        try
        {
            var picker = new FileOpenPicker(App.MainWindow.AppWindow.Id);
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file is not null && IsCurrent())
            {
                parameter.TextValue = file.Path;
            }
        }
        catch (Exception ex)
        {
            if (IsCurrent())
            {
                Logger.LogError("Unable to select a PowerToys operation file.", ex);
                CreationStatus.Severity = InfoBarSeverity.Error;
                CreationStatus.Message = ex.Message;
                CreationStatus.IsOpen = true;
            }
        }
        finally
        {
            button.IsEnabled = true;
            _isBrowsing = false;
        }

        bool IsCurrent() => _isActive && generation == _pageGeneration && revision == _draftRevision &&
            _draft?.Parameters.Contains(parameter) == true && parameter.IsValueProvided;
    }

    private async void TestRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isTesting)
        {
            _testCancellation?.Cancel();
            TestStatus.Text = Resource("PowerScripts_RunCancelRequested");
            return;
        }

        var draft = _draft;
        var template = SelectedTemplate;
        if (draft is null || template is null || _isCreating || _isLoading ||
            !string.IsNullOrWhiteSpace(template.UnavailableReason))
        {
            return;
        }

        if (!draft.TryBuildConfiguration(out var parameters, out var operation, out var error) ||
            !draft.TryGetTestValues(out var values, out error))
        {
            ValidationMessage.Text = error;
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }

        var generation = _pageGeneration;
        var revision = _draftRevision;
        using var cancellation = new CancellationTokenSource();
        _testCancellation = cancellation;
        _isTesting = true;
        ResetTestResult();
        TestStatus.Text = Resource("PowerScripts_RunRunning");
        TestStatus.Visibility = Visibility.Visible;
        UpdateState();
        var showResult = false;
        try
        {
            var result = await _viewModel.TestOperationAsync(template, operation, parameters, values, cancellation.Token);
            if (IsCurrent())
            {
                TestResultTitle.Text = Resource(result.Succeeded ? "PowerScripts_RunSucceeded" : "PowerScripts_RunFailed") + " · " +
                    Resource("PowerScripts_RunExitCode").Replace("{0}", result.ExitCode.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);
                var output = new StringBuilder();
                if (!string.IsNullOrEmpty(result.Text))
                {
                    output.Append(result.Text);
                }

                if (!string.IsNullOrWhiteSpace(result.StdErr))
                {
                    if (output.Length > 0)
                    {
                        output.AppendLine().AppendLine();
                    }

                    output.AppendLine(Resource("PowerScripts_RunDiagnostics.Header")).Append(result.StdErr);
                }

                TestOutputTextBox.Text = output.Length == 0 ? Resource("PowerScripts_OperationsNoOutput") : output.ToString();
                TestStatus.Text = TestResultTitle.Text;
                showResult = true;
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent())
            {
                TestResultTitle.Text = Resource(cancellation.IsCancellationRequested ? "PowerScripts_RunCancelled" : "PowerScripts_RunTimedOut");
                TestOutputTextBox.Text = Resource("PowerScripts_RunExecutionUnknown");
                TestStatus.Text = TestResultTitle.Text;
                showResult = true;
            }
        }
        catch (Exception ex)
        {
            if (IsCurrent())
            {
                Logger.LogError("Unable to test a PowerToys operation draft.", ex);
                TestResultTitle.Text = Resource("PowerScripts_RunFailed");
                TestOutputTextBox.Text = ex.Message;
                TestStatus.Text = TestResultTitle.Text;
                showResult = true;
            }
        }
        finally
        {
            if (ReferenceEquals(_testCancellation, cancellation))
            {
                _testCancellation = null;
                _isTesting = false;
            }

            if (IsCurrent())
            {
                UpdateState();
                if (showResult)
                {
                    TestResultFlyout.ShowAt(TestRunButton);
                }
            }
        }

        bool IsCurrent() => _isActive && generation == _pageGeneration && revision == _draftRevision && ReferenceEquals(draft, _draft);
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_draft is null || _isCreating || _isTesting || _isLoading ||
            !_draft.TryBuildConfiguration(out var parameters, out var operation, out _))
        {
            return;
        }

        var generation = _pageGeneration;
        var draft = _draft;
        _isCreating = true;
        CreationStatus.IsOpen = false;
        UpdateState();
        var created = false;
        try
        {
            await _viewModel.CreateTemplateAsync(
                ProjectNameTextBox.Text,
                SelectedTemplate.Description,
                "powertoys-cli",
                "none",
                "text",
                null,
                1,
                0,
                parameters,
                operation);
            created = true;
        }
        catch (Exception ex)
        {
            if (_isActive && generation == _pageGeneration)
            {
                Logger.LogError("Unable to create a PowerToys operation script.", ex);
                CreationStatus.Severity = InfoBarSeverity.Error;
                CreationStatus.Message = ex.Message;
                CreationStatus.IsOpen = true;
            }
        }
        finally
        {
            _isCreating = false;
            if (_isActive && generation == _pageGeneration)
            {
                UpdateState();
            }
        }

        if (created && _isActive && generation == _pageGeneration && ReferenceEquals(draft, _draft))
        {
            if (!EditorNavigation.GoBack(Frame))
            {
                Frame.Navigate(typeof(MyScriptsPage), _viewModel);
            }
        }
    }
}

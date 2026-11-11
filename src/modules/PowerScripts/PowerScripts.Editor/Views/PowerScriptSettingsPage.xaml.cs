// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.Client;
using PowerScripts.Editor.Helpers;
using PowerScripts.UI.Helpers;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WinUI owns the Page lifecycle. OnNavigatedFrom cancels and disposes navigation cancellation; OnNavigatedTo releases any previous lifetime before replacing it.")]
public sealed partial class PowerScriptSettingsPage : Page
{
    private NavigationContext _context;
    private bool _isLoading;
    private bool _isRunLoading;
    private bool _isActive;
    private int _runGeneration;
    private CancellationTokenSource _pageCancellation;
    private CancellationTokenSource _runCancellation;
    private PowerScriptsClient _runClient;

    public PowerScriptRunViewModel RunModel { get; } = new();

    public PowerScriptSettingsPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        RunModel.PropertyChanged += RunModel_PropertyChanged;
    }

    public sealed record NavigationContext(
        PowerScriptsViewModel ViewModel,
        PowerScriptListItem Script);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _context = e.Parameter as NavigationContext ??
            throw new ArgumentException("Script settings navigation requires a script and its view model.", nameof(e));
        ReleaseNavigationCancellation();
        _isActive = true;
        _pageCancellation = new CancellationTokenSource();
        DataContext = _context.Script;
        _context.Script.PropertyChanged += Script_PropertyChanged;
        UpdateBreadcrumb();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        _runGeneration++;
        _runCancellation?.Cancel();
        ReleaseNavigationCancellation();
        _context.Script.PropertyChanged -= Script_PropertyChanged;
        RunModel.PropertyChanged -= RunModel_PropertyChanged;
        base.OnNavigatedFrom(e);
    }

    private void ReleaseNavigationCancellation()
    {
        var cancellation = _pageCancellation;
        _pageCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await RefreshMetadataAsync();

    private async void RefreshMetadataButton_Click(object sender, RoutedEventArgs e) => await RefreshMetadataAsync();

    private async Task RefreshMetadataAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        MetadataCommands.IsEnabled = false;
        try
        {
            var entryPath = _context.Script.EntryFullPath;
            await _context.ViewModel.ReloadScriptsAsync(preserveEditorOrder: true);
            var current = _context.ViewModel.Scripts.FirstOrDefault(script => string.Equals(script.EntryFullPath, entryPath, StringComparison.OrdinalIgnoreCase));
            if (current is not null && !ReferenceEquals(current, _context.Script))
            {
                _context.Script.PropertyChanged -= Script_PropertyChanged;
                _context = _context with { Script = current };
                DataContext = current;
                current.PropertyChanged += Script_PropertyChanged;
                UpdateBreadcrumb();
            }

            var descriptor = JsonNode.Parse(await File.ReadAllTextAsync(entryPath + ".tool.json")) as JsonObject
                ?? throw new InvalidDataException("Metadata must be a JSON object.");
            MetadataSections.ItemsSource = PowerScriptMetadataDetails.Create(descriptor, ResourceLoaderInstance.ResourceLoader.GetString, Path.GetExtension(entryPath));
            MetadataText.Text = descriptor.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            if (_context.Script.HasConfigurationErrors)
            {
                ShowError(new InvalidDataException(_context.Script.ConfigurationErrorDisplay));
            }
            else
            {
                ScriptSettingsError.IsOpen = false;
            }
        }
        catch (Exception ex)
        {
            MetadataSections.ItemsSource = Array.Empty<PowerScriptMetadataSection>();
            MetadataText.Text = string.Empty;
            ShowError(ex);
        }
        finally
        {
            _isLoading = false;
            UpdateCommands();
        }
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private async void RunCommandButton_Click(object sender, RoutedEventArgs e)
    {
        RunExpander.Visibility = Visibility.Visible;
        RunExpander.IsExpanded = true;
        if (!RunModel.IsRunning)
        {
            await LoadRunContractAsync();
        }
    }

    private async Task<bool> LoadRunContractAsync()
    {
        if (!_isActive || _isRunLoading || RunModel.IsRunning)
        {
            return false;
        }

        var generation = ++_runGeneration;
        _isRunLoading = true;
        RunModel.StartLoading();
        UpdateCommands();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_pageCancellation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var client = await _context.ViewModel.CreateRunClientAsync(timeout.Token);
            var scripts = await client.ListAsync(timeout.Token);
            if (!_isActive || generation != _runGeneration)
            {
                return false;
            }

            var script = scripts.FirstOrDefault(script =>
                string.Equals(script.Id, _context.Script.Id, StringComparison.Ordinal) &&
                string.Equals(script.EntryFullPath, _context.Script.EntryFullPath, StringComparison.OrdinalIgnoreCase));
            if (script is null)
            {
                throw new InvalidOperationException(Resource("PowerScripts_RunUnavailable"));
            }

            _runClient = client;
            RunModel.FinishLoading(script);
            return true;
        }
        catch (OperationCanceledException)
        {
            if (_isActive && generation == _runGeneration)
            {
                RunModel.LoadingFailed(Resource("PowerScripts_RunDiscoveryTimeout"));
            }

            return false;
        }
        catch (Exception ex)
        {
            if (_isActive && generation == _runGeneration)
            {
                RunModel.LoadingFailed(ex.Message);
            }

            return false;
        }
        finally
        {
            _isRunLoading = false;
            UpdateCommands();
        }
    }

    private async void RunNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RunModel.CanRun)
        {
            return;
        }

        var previous = RunModel.Script;
        if (!await LoadRunContractAsync())
        {
            return;
        }

        if (!PowerScriptParameterValues.HasSameInputContract(previous, RunModel.Script))
        {
            RunModel.Fail(Resource("PowerScripts_RunContractChanged"));
            return;
        }

        if (!RunModel.TryCreateInput(out var input, out var error))
        {
            RunModel.Fail(error);
            return;
        }

        var generation = ++_runGeneration;
        using var stop = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, timeout.Token, _pageCancellation.Token);
        _runCancellation = stop;
        RunModel.BeginRun();
        UpdateCommands();
        try
        {
            // Host owns enablement, consumer preferences, content trust, execution and isolation.
            // Explicit Run keeps its normal consent prompt; the editor never pre-approves content.
            var result = await _runClient.TransformAsync(
                RunModel.Script.Id,
                input,
                noConsent: false,
                cancellationToken: linked.Token);
            if (_isActive && generation == _runGeneration)
            {
                RunModel.Complete(result);
            }
        }
        catch (OperationCanceledException)
        {
            if (_isActive && generation == _runGeneration)
            {
                // Cancellation confirms neither rollback nor the state of external side effects.
                RunModel.Cancelled(timeout.IsCancellationRequested && !stop.IsCancellationRequested);
            }
        }
        catch (Exception ex)
        {
            if (_isActive && generation == _runGeneration)
            {
                RunModel.Fail(ex.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(_runCancellation, stop))
            {
                _runCancellation = null;
            }

            UpdateCommands();
        }
    }

    private void CancelRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (RunModel.IsRunning && _runCancellation is not null)
        {
            RunModel.RequestCancellation();
            _runCancellation.Cancel();
        }
    }

    private async void BrowseParameterFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PowerScriptRunParameter parameter } || !RunModel.CanEdit)
        {
            return;
        }

        var generation = _runGeneration;
        try
        {
            var file = await CreateFilePicker().PickSingleFileAsync();
            if (file is not null && _isActive && generation == _runGeneration && RunModel.Parameters.Contains(parameter))
            {
                parameter.TextValue = file.Path;
                parameter.IsProvided = true;
            }
        }
        catch (Exception ex)
        {
            if (_isActive && generation == _runGeneration)
            {
                RunModel.Fail(ex.Message);
            }
        }
    }

    private async void AddInputFileButton_Click(object sender, RoutedEventArgs e)
    {
        var generation = _runGeneration;
        try
        {
            var file = await CreateFilePicker().PickSingleFileAsync();
            if (file is not null && _isActive && generation == _runGeneration && RunModel.CanEdit)
            {
                RunModel.PrimaryFilesText = string.IsNullOrWhiteSpace(RunModel.PrimaryFilesText)
                    ? file.Path
                    : RunModel.PrimaryFilesText + Environment.NewLine + file.Path;
            }
        }
        catch (Exception ex)
        {
            if (_isActive && generation == _runGeneration)
            {
                RunModel.Fail(ex.Message);
            }
        }
    }

    private static Microsoft.Windows.Storage.Pickers.FileOpenPicker CreateFilePicker()
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.MainWindow.AppWindow.Id);
        picker.FileTypeFilter.Add("*");
        return picker;
    }

    private void RunModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PowerScriptRunViewModel.IsRunning) or nameof(PowerScriptRunViewModel.IsLoading))
        {
            UpdateCommands();
        }
    }

    private void UpdateCommands()
    {
        MetadataCommands.IsEnabled = _isActive && !_isLoading && !_isRunLoading && !RunModel.IsRunning;
        RunCommandButton.IsEnabled = _context?.ViewModel.IsEnabled == true && !_context.Script.HasConfigurationErrors;
    }

    private static string Resource(string name) => PowerScriptsViewModel.GetResourceString(name);

    private void EditMetadataButton_Click(object sender, RoutedEventArgs e) => OpenEditor(_context.Script.EntryFullPath + ".tool.json", isMetadata: true);

    private void EditScriptButton_Click(object sender, RoutedEventArgs e) => OpenEditor(_context.Script.EntryFullPath, isMetadata: false);

    private void OpenEditor(string path, bool isMetadata)
    {
        try
        {
            EditorStatus.IsOpen = PowerScriptFileEditor.Open(path, isMetadata);
            ScriptSettingsError.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void Script_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PowerScriptListItem.Name))
        {
            UpdateBreadcrumb();
        }
    }

    private void UpdateBreadcrumb() =>
        ScriptBreadcrumb.ItemsSource = new[] { "PowerScripts", _context.Script.Name };

    private void BackButton_Click(object sender, RoutedEventArgs e) => EditorNavigation.GoBack(Frame);

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = EditorNavigation.GoBack(Frame);

    private void ScriptBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0)
        {
            EditorNavigation.GoBack(Frame);
        }
    }

    private void OpenScriptFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var script = _context.Script;
        try
        {
            if (!string.IsNullOrEmpty(script.EntryFullPath) && File.Exists(script.EntryFullPath))
            {
                Process.Start("explorer.exe", $"/select,\"{script.EntryFullPath}\"");
            }
            else if (Directory.Exists(script.FolderPath))
            {
                Process.Start("explorer.exe", $"\"{script.FolderPath}\"");
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ShowError(Exception exception)
    {
        Logger.LogError("Unable to change PowerScripts settings.", exception);
        ScriptSettingsError.Message = exception.Message;
        ScriptSettingsError.IsOpen = true;
    }
}

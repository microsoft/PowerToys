// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.Client;
using PowerScripts.Editor.Helpers;
using PowerScripts.UI.Helpers;
using PowerScripts.UI.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PowerScripts.Editor.Views;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WinUI owns the Page lifecycle. Navigation cancels execution and disposes cancellation and reusable Host clients before replacing the lifetime.")]
public sealed partial class PowerScriptSettingsPage : Page
{
    private const string SettingsIconFolder = "ms-appx:///Assets/Settings/Icons/";
    private const double WideLayoutMinimumWidth = 1000;
    private const double SideColumnWidth = 340;
    private const double BodyColumnSpacing = 24;
    private const int RecentRunCount = 3;

    private readonly Dictionary<string, BitmapImage> _images = new(StringComparer.OrdinalIgnoreCase);

    private NavigationContext _context;
    private bool _isLoading;
    private bool _isRunLoading;
    private bool _isActive;
    private int _runGeneration;
    private CancellationTokenSource _pageCancellation;
    private CancellationTokenSource _runCancellation;
    private PowerScriptsClient _runClient;
    private string _runClientRoot;

    public PowerScriptRunViewModel RunModel { get; } = new();

    public ObservableCollection<PowerScriptPreviewRow> Places { get; } = new();

    public ObservableCollection<PowerScriptPreviewRow> UnavailablePlaces { get; } = new();

    private ObservableCollection<PowerScriptActivityItem> RecentRuns { get; } = new();

    public PowerScriptSettingsPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        RunModel.PropertyChanged += RunModel_PropertyChanged;
    }

    public sealed record NavigationContext(
        PowerScriptsViewModel ViewModel,
        PowerScriptListItem Script);

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility TextVisibility(string value) => BoolToVisibility(!string.IsNullOrEmpty(value));

    public static Visibility RunInputsVisibility(bool hasTextInput, bool hasFileInput, bool hasNoParameters) =>
        BoolToVisibility(hasTextInput || hasFileInput || !hasNoParameters);

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
        _context.ViewModel.ActivityItems.CollectionChanged += ActivityItems_CollectionChanged;
        UpdateHeaderIcon();
        UpdateScriptDetails();
        UpdateRecentRuns();
        UpdateCommands();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        _runGeneration++;
        _runCancellation?.Cancel();
        ReleaseNavigationCancellation();
        _context.Script.PropertyChanged -= Script_PropertyChanged;
        _context.ViewModel.ActivityItems.CollectionChanged -= ActivityItems_CollectionChanged;
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
            _runClient?.Dispose();
            _runClient = null;
            _runClientRoot = null;
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshMetadataAsync();
        _ = LoadRunContractAsync();
        await RefreshActivityAsync();
    }

    private async void RefreshMetadataButton_Click(object sender, RoutedEventArgs e) => await RefreshMetadataAsync();

    private async Task RefreshMetadataAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        UpdateCommands();
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
                UpdateHeaderIcon();
                UpdateScriptDetails();
                UpdateRecentRuns();
            }

            var descriptor = JsonNode.Parse(await File.ReadAllTextAsync(entryPath + ".tool.json")) as JsonObject
                ?? throw new InvalidDataException("Metadata must be a JSON object.");
            MetadataText.Text = descriptor.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            using (var reader = new StreamReader(entryPath))
            {
                var preview = new char[(256 * 1024) + 1];
                var count = await reader.ReadBlockAsync(preview.AsMemory());
                SourceText.Text = count == preview.Length
                    ? Resource("PowerScripts_DetailsSourceUnavailable")
                    : new string(preview, 0, count);
            }

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
            MetadataText.Text = string.Empty;
            SourceText.Text = string.Empty;
            ShowError(ex);
        }
        finally
        {
            _isLoading = false;
            UpdateCommands();
        }
    }

    private async Task RefreshActivityAsync()
    {
        try
        {
            await _context.ViewModel.RefreshActivityAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Unable to refresh PowerScripts activity: {ex.Message}");
        }
    }

    private async void RunCommandButton_Click(object sender, RoutedEventArgs e)
    {
        RunCard.StartBringIntoView();
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
            var root = _context.ViewModel.ScriptsFolder;
            if (_runClient is not null && !string.Equals(_runClientRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                _runClient.Dispose();
                _runClient = null;
            }

            var client = _runClient ?? await _context.ViewModel.CreateRunClientAsync(timeout.Token);
            if (!_isActive || generation != _runGeneration)
            {
                client.Dispose();
                return false;
            }

            _runClient = client;
            _runClientRoot = root;
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

        if (_isActive)
        {
            await RefreshActivityAsync();
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

        ResultPlaceholder.Visibility = BoolToVisibility(!RunModel.HasOutput && !RunModel.HasDiagnostics && !RunModel.HasFailure);
    }

    private void UpdateCommands()
    {
        var canManage = _isActive && !_isLoading && !_isRunLoading && !RunModel.IsRunning;
        HeroEditScriptButton.IsEnabled = canManage;
        CopyAgentInstructionsButton.IsEnabled = canManage;
        MoreButton.IsEnabled = canManage;
        CodeEditButton.IsEnabled = canManage;
        RunCommandButton.IsEnabled = canManage && _context?.ViewModel.IsEnabled == true && !_context.Script.HasConfigurationErrors;
    }

    private static string Resource(string name) => PowerScriptsViewModel.GetResourceString(name);

    private static string Format(string name, object value) => string.Format(CultureInfo.CurrentCulture, Resource(name), value);

    private void HeroEditScriptButton_Click(SplitButton sender, SplitButtonClickEventArgs args) => OpenEditor(_context.Script.EntryFullPath, isMetadata: false);

    private void EditMetadataButton_Click(object sender, RoutedEventArgs e) => OpenEditor(_context.Script.EntryFullPath + ".tool.json", isMetadata: true);

    private void CodeEditButton_Click(object sender, RoutedEventArgs e)
    {
        var isMetadata = CodeSwitch.SelectedIndex == 1;
        OpenEditor(isMetadata ? _context.Script.EntryFullPath + ".tool.json" : _context.Script.EntryFullPath, isMetadata);
    }

    private void CodeSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceText is null || MetadataText is null)
        {
            return;
        }

        var isMetadata = CodeSwitch.SelectedIndex == 1;
        SourceText.Visibility = BoolToVisibility(!isMetadata);
        MetadataText.Visibility = BoolToVisibility(isMetadata);
    }

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
        switch (e.PropertyName)
        {
            case nameof(PowerScriptListItem.HasIcon):
                UpdateHeaderIcon();
                break;
            case nameof(PowerScriptListItem.Io):
            case nameof(PowerScriptListItem.Input):
            case nameof(PowerScriptListItem.Capabilities):
            case nameof(PowerScriptListItem.Trusted):
            case nameof(PowerScriptListItem.EntryFullPath):
            case null:
            case "":
                UpdateScriptDetails();
                break;
        }
    }

    private void ActivityItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => UpdateRecentRuns();

    private void UpdateScriptDetails()
    {
        UpdatePlaces();
        UpdateSafety();
        UpdateFiles();
    }

    private void UpdatePlaces()
    {
        var script = _context.Script;
        var viewModel = _context.ViewModel;
        var input = script.Io?.Input?.ToLowerInvariant() ?? string.Empty;
        var output = script.Io?.Output?.ToLowerInvariant() ?? string.Empty;
        var hasInput = input != "none";
        var knownIo = IsKnownIo(input) && IsKnownIo(output);
        var available = new List<PowerScriptPreviewRow>();
        var unavailable = new List<PowerScriptPreviewRow>();

        void AddPlace(string id, string icon, PowerScriptsViewModel.PowerScriptConsumerItem consumer, bool eligible, Func<string> detail, string whyKey)
        {
            eligible &= knownIo;
            var rows = eligible ? available : unavailable;
            rows.Add(new PowerScriptPreviewRow
            {
                Title = Resource("PowerScripts_NewScript_Place_" + id),
                Detail = eligible ? detail() : Resource("PowerScripts_NewScript_" + whyKey),
                Icon = GetImage(SettingsIconFolder + icon + ".png"),
                IsOff = eligible && (!viewModel.IsEnabled || !consumer.IsEnabled),
                HasDivider = rows.Count > 0,
            });
        }

        AddPlace(
            "AdvancedPaste",
            "AdvancedPaste",
            viewModel.AdvancedPasteConsumer,
            hasInput && output != "none",
            () => Format("PowerScripts_NewScript_PlaceDetail_AdvancedPaste", Resource("PowerScripts_NewScript_TakesWord_" + input)),
            hasInput ? "PlaceWhy_AdvancedPasteOutput" : "PlaceWhy_AdvancedPaste");
        AddPlace(
            "KeyboardManager",
            "KeyboardManager",
            viewModel.KeyboardManagerConsumer,
            !hasInput,
            () => Resource("PowerScripts_NewScript_PlaceDetail_KeyboardManager"),
            "PlaceWhy_KeyboardManager");
        AddPlace(
            "CommandPalette",
            "CmdPal",
            viewModel.CommandPaletteConsumer,
            !hasInput && output is "none" or "text",
            () => Resource("PowerScripts_NewScript_PlaceDetail_CommandPalette"),
            hasInput ? "PlaceWhy_CommandPalette" : "PlaceWhy_CommandPaletteOutput");
        AddPlace(
            "LightSwitch",
            "LightSwitch",
            viewModel.LightSwitchConsumer,
            !hasInput && output == "none",
            () => Resource("PowerScripts_NewScript_PlaceDetail_LightSwitch"),
            "PlaceWhy_LightSwitch");
        AddPlace(
            "FileExplorer",
            "FileExplorerPreview",
            viewModel.FileExplorerConsumer,
            input is "files" or "image" or "audio",
            () => Format("PowerScripts_NewScript_PlaceDetail_FileExplorer", ExplorerExtensions(script, input)),
            "PlaceWhy_FileExplorer");

        ReplaceRows(Places, available);
        ReplaceRows(UnavailablePlaces, unavailable);
        PlacesEmpty.Visibility = BoolToVisibility(Places.Count == 0);
        UnavailableExpander.Visibility = BoolToVisibility(UnavailablePlaces.Count > 0);
    }

    private static bool IsKnownIo(string value) => value is "none" or "text" or "files" or "image" or "audio";

    private static string ExplorerExtensions(PowerScriptListItem script, string input)
    {
        var extensions = script.Input?.Extensions;
        if (input != "files" || extensions is not { Count: > 0 } || extensions.Contains("*"))
        {
            return string.Empty;
        }

        return string.Join(", ", extensions) + " ";
    }

    private static void ReplaceRows(ObservableCollection<PowerScriptPreviewRow> target, List<PowerScriptPreviewRow> rows)
    {
        target.Clear();
        foreach (var row in rows)
        {
            target.Add(row);
        }
    }

    private BitmapImage GetImage(string uri)
    {
        if (!_images.TryGetValue(uri, out var image))
        {
            image = new BitmapImage(new Uri(uri));
            _images.Add(uri, image);
        }

        return image;
    }

    private void UpdateSafety()
    {
        var script = _context.Script;
        TrustIcon.Glyph = script.Trusted ? "\uEA18" : "\uE7BA";
        TrustIcon.Foreground = (Brush)Application.Current.Resources[script.Trusted ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush"];
        TrustText.Text = Resource(script.Trusted ? "PowerScripts_Script_Allowed" : "PowerScripts_Script_NotAllowed");

        var capabilities = script.Capabilities ?? new List<string>();
        CapabilitiesText.Text = string.Join(", ", capabilities.Select(CapabilityDisplay));
        CapabilitiesRow.Visibility = BoolToVisibility(capabilities.Count > 0);
    }

    private static string CapabilityDisplay(string capability) => capability switch
    {
        "fileRead" => Resource("PowerScripts_DetailsFileRead"),
        "fileWrite" => Resource("PowerScripts_DetailsFileWrite"),
        "network" => Resource("PowerScripts_DetailsNetwork"),
        "ui" => Resource("PowerScripts_DetailsUi"),
        "hostPowerToysCli" => Resource("PowerScripts_DetailsHostCli"),
        _ => capability,
    };

    private void UpdateFiles()
    {
        var name = Path.GetFileName(_context.Script.EntryFullPath);
        ScriptFileItem.Content = name;
        FileNames.Text = string.IsNullOrEmpty(name) ? string.Empty : name + Environment.NewLine + name + ".tool.json";
    }

    private void UpdateRecentRuns()
    {
        if (_context is null)
        {
            return;
        }

        var runs = _context.ViewModel.ActivityItems
            .Where(item => string.Equals(item.ScriptId, _context.Script.Id, StringComparison.OrdinalIgnoreCase))
            .Take(RecentRunCount)
            .ToList();
        if (!RecentRuns.SequenceEqual(runs))
        {
            RecentRuns.Clear();
            foreach (var run in runs)
            {
                RecentRuns.Add(run);
            }
        }

        SeeAllButton.Visibility = BoolToVisibility(runs.Count > 0);
        RecentEmpty.Visibility = BoolToVisibility(runs.Count == 0);
    }

    private void SeeAll_Click(object sender, RoutedEventArgs e) => App.MainWindow.ShowActivity();

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(_context.Script.EntryFullPath);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void BodyGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= WideLayoutMinimumWidth;
        SideColumn.Width = new GridLength(wide ? SideColumnWidth : 0);
        BodyGrid.ColumnSpacing = wide ? BodyColumnSpacing : 0;
        Grid.SetRow(SidePanel, wide ? 0 : 1);
        Grid.SetColumn(SidePanel, wide ? 1 : 0);
    }

    private void UpdateHeaderIcon() =>
        HeaderDefaultIcon.Visibility = _context.Script.HasIcon ? Visibility.Collapsed : Visibility.Visible;

    private void HeaderIcon_ImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        Logger.LogWarning($"Unable to display PowerScript icon '{_context.Script.IconPath}': {args.ErrorMessage}");
        _context.Script.MarkIconUnavailable();
    }

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = EditorNavigation.GoBack(Frame);

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

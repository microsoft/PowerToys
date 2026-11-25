// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.Editor.Helpers;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

/// <summary>Creates a script from what it takes, what runs it, and what it gives back.</summary>
public sealed partial class PowerScriptTemplatesPage : Page
{
    private const string Separator = " \u00B7 ";
    private const string SettingsIconFolder = "ms-appx:///Assets/Settings/Icons/";
    private const string DefaultGlyph = "\uE756";
    private const double WideLayoutMinimumWidth = 1000;
    private const double SideColumnWidth = 340;
    private const double BodyColumnSpacing = 24;

    private static readonly string[] ShapeTags = new[] { "none", "text", "files", "image", "audio" };
    private static readonly string[] RuntimeTags = new[] { "powershell", "python", "pt" };
    private static readonly char[] ExtensionSeparators = new[] { ',', ';', ' ', '\t', '\r', '\n' };

    private static readonly HashSet<string> ReservedParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "args", "input", "PSItem", "this", "PSBoundParameters", "PSCmdlet", "ExecutionContext",
        "Host", "HOME", "PID", "PWD", "PSVersionTable", "PSScriptRoot", "PSCommandPath", "MyInvocation", "Error", "Matches",
        "foreach", "switch", "ShellId", "EnabledExperimentalFeatures", "IsWindows", "IsLinux", "IsMacOS", "IsCoreCLR",
        "PSCulture", "PSUICulture", "PSHOME", "NestedPromptLevel", "StackTrace", "ConsoleFileName", "PSDebugContext",
        "ErrorActionPreference",
    };

    private readonly Dictionary<string, string> _selectedCliActions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BitmapImage> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _modulePlaceholder;

    private bool _isCreating;
    private bool _isActive;
    private bool _loadingCliContent;
    private bool _updatingCliSelection;
    private bool _updatingPickers;
    private bool _syncingCliSources;
    private bool _validating;
    private bool _updateQueued;
    private bool _nameTouched;
    private int _nextParameterId;
    private string _customIconPath;
    private string _iconPreset;
    private string _input = "none";
    private string _output = "text";
    private string _runtime = "powershell";

    public PowerScriptTemplatesPage()
    {
        InitializeComponent();
        _modulePlaceholder = CliModuleComboBox.PlaceholderText;
        BuildPicker(InputPicker, ShapeTags, "Input");
        BuildPicker(RuntimePicker, RuntimeTags, "Runtime");
        BuildPicker(OutputPicker, ShapeTags, "Output");
        BuildIconMenu();
        TileImage.ImageFailed += (_, _) => ShowTileGlyph();
        RunsImage.ImageFailed += (_, _) =>
        {
            RunsImage.Visibility = Visibility.Collapsed;
            RunsGlyph.Visibility = Visibility.Visible;
        };
    }

    public PowerScriptsViewModel ViewModel { get; private set; }

    public ObservableCollection<PowerScriptTemplateParameter> Parameters { get; } = new();

    public ObservableCollection<PowerScriptPreviewRow> Places { get; } = new();

    public ObservableCollection<PowerScriptPreviewRow> UnavailablePlaces { get; } = new();

    public ObservableCollection<PowerScriptPreviewRow> AskRows { get; } = new();

    internal bool IsCreating => _isCreating;

    private bool IsCliContent => _runtime == "pt";

    private PowerScriptCliModule SelectedCliModule => (CliModuleComboBox.SelectedItem as ComboBoxItem)?.Tag as PowerScriptCliModule;

    private PowerScriptOperationTemplate SelectedCliContent =>
        IsCliContent ? (CliContentComboBox.SelectedItem as ComboBoxItem)?.Tag as PowerScriptOperationTemplate : null;

    private IEnumerable<PowerScriptsViewModel.PowerScriptConsumerItem> Consumers => new[]
    {
        ViewModel.AdvancedPasteConsumer,
        ViewModel.KeyboardManagerConsumer,
        ViewModel.FileExplorerConsumer,
        ViewModel.CommandPaletteConsumer,
    };

    public static Thickness DividerThickness(bool hasDivider) => hasDivider ? new Thickness(0, 1, 0, 0) : new Thickness(0);

    public static double OffOpacity(bool isOff) => isOff ? 0.6 : 1.0;

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility HasTypeVisibility(int typeIndex) => typeIndex >= 0 ? Visibility.Visible : Visibility.Collapsed;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel ?? throw new ArgumentException("Creation navigation requires the PowerScripts view model.", nameof(e));
        _isActive = true;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        foreach (var consumer in Consumers)
        {
            consumer.PropertyChanged += Consumer_PropertyChanged;
        }

        SelectPickerItem(InputPicker, _input);
        SelectPickerItem(RuntimePicker, _runtime);
        SelectPickerItem(OutputPicker, _output);
        UpdateOptions();
        if (IsCliContent && CliModuleComboBox.Items.Count == 0)
        {
            _ = LoadCliContentAsync();
        }

        DispatcherQueue.TryEnqueue(() => ProjectNameTextBox.Focus(FocusState.Programmatic));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            foreach (var consumer in Consumers)
            {
                consumer.PropertyChanged -= Consumer_PropertyChanged;
            }
        }

        CliModuleComboBox.IsDropDownOpen = false;
        CliContentComboBox.IsDropDownOpen = false;
        base.OnNavigatedFrom(e);
    }

    private static string GetString(string key) => PowerScriptsViewModel.GetResourceString(key);

    private static string Format(string key, object value) => string.Format(CultureInfo.CurrentCulture, GetString(key), value);

    private static string PickerGlyph(string kind, string tag) => (kind, tag) switch
    {
        ("Runtime", "python") => "\uE943",
        ("Runtime", _) => DefaultGlyph,
        ("Output", "none") => "\uE73E",
        (_, "none") => "\uE768",
        (_, "text") => "\uE8D2",
        (_, "files") => "\uE8A5",
        (_, "image") => "\uEB9F",
        (_, "audio") => "\uE8D6",
        _ => DefaultGlyph,
    };

    private static bool IsValidParameterName(string name) =>
        name.Length > 0 && char.IsAsciiLetter(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static bool TryParseInteger(string text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    // Mirrors PowerScripts.Core's template extension rules so mistakes show before Create is pressed.
    private static bool TryParseFileExtensions(string text, out string[] extensions)
    {
        extensions = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            extensions = new[] { "*" };
            return true;
        }

        if (text.Length > 1024)
        {
            return false;
        }

        var values = text.Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("*.", StringComparison.Ordinal) ? value[1..] : value)
            .Select(value => value == "*" || value.StartsWith('.') ? value : "." + value)
            .Select(value => value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length is 0 or > 32 ||
            (values.Contains("*") && values.Length != 1) ||
            values.Any(value => value != "*" &&
                (value.Length is < 2 or > 32 || value[1..].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))))
        {
            return false;
        }

        extensions = values;
        return true;
    }

    private static bool IsWholeCount(double value) =>
        double.IsFinite(value) && value >= 0 && value <= int.MaxValue && Math.Floor(value) == value;

    private static int FileCount(NumberBox box)
    {
        if (!IsWholeCount(box.Value))
        {
            throw new ArgumentException(GetString("PowerScripts_TemplateInvalidFileCount"));
        }

        return (int)box.Value;
    }

    private static bool TryValidateParameter(PowerScriptTemplateParameter parameter, HashSet<string> names, out string error)
    {
        error = null;
        if (parameter.TypeIndex < 0)
        {
            error = GetString("PowerScripts_NewScript_TypeRequired");
            return false;
        }

        var name = parameter.Name ?? string.Empty;
        if (name.Length == 0)
        {
            return false;
        }

        if (!IsValidParameterName(name) || ReservedParameterNames.Contains(name) || !names.Add(name))
        {
            error = GetString("PowerScripts_NewScript_ParameterNameInvalid");
            return false;
        }

        if (parameter.HasDefault && !parameter.IsBoolean && string.IsNullOrWhiteSpace(parameter.DefaultText))
        {
            error = Format("PowerScripts_TemplateParameterValueRequired", name);
            return false;
        }

        if (parameter.IsInteger)
        {
            if (!TryParseInteger(parameter.MinimumText, out var minimum) || !TryParseInteger(parameter.MaximumText, out var maximum))
            {
                error = Format("PowerScripts_TemplateParameterInvalidInteger", name);
                return false;
            }

            if (minimum > maximum)
            {
                error = Format("PowerScripts_TemplateParameterOutsideRange", name);
                return false;
            }

            if (parameter.HasDefault)
            {
                if (!TryParseInteger(parameter.DefaultText, out var value))
                {
                    error = Format("PowerScripts_TemplateParameterInvalidInteger", name);
                    return false;
                }

                if (value < minimum || value > maximum)
                {
                    error = Format("PowerScripts_TemplateParameterOutsideRange", name);
                    return false;
                }
            }
        }
        else if (parameter.HasDefault && parameter.IsString)
        {
            var choices = parameter.GetChoices();
            if (choices.Length > 0 && !choices.Contains(parameter.DefaultText, StringComparer.Ordinal))
            {
                error = Format("PowerScripts_TemplateParameterInvalidChoice", name);
                return false;
            }
        }

        if (parameter.IsFile && !TryParseFileExtensions(parameter.FileExtensions, out _))
        {
            error = GetString("PowerScripts_NewScript_ExtensionsInvalid");
            return false;
        }

        return true;
    }

    private static bool SameRow(PowerScriptPreviewRow first, PowerScriptPreviewRow second) =>
        first.Title == second.Title &&
        first.Detail == second.Detail &&
        first.Glyph == second.Glyph &&
        first.TileKind == second.TileKind &&
        first.IsOff == second.IsOff &&
        first.HasDivider == second.HasDivider &&
        ReferenceEquals(first.Icon, second.Icon);

    // Rows aren't observable, so swap them only when something visible changed to avoid churn while typing.
    private static void ReplaceRows(ObservableCollection<PowerScriptPreviewRow> target, List<PowerScriptPreviewRow> rows)
    {
        if (target.Count == rows.Count && target.Zip(rows).All(pair => SameRow(pair.First, pair.Second)))
        {
            return;
        }

        target.Clear();
        foreach (var row in rows)
        {
            target.Add(row);
        }
    }

    private static void SetPickerItemsEnabled(GridView picker, Func<string, bool> isEnabled)
    {
        foreach (var item in picker.Items.OfType<GridViewItem>())
        {
            item.IsEnabled = isEnabled((string)item.Tag);
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

    private void BuildPicker(GridView picker, string[] tags, string kind)
    {
        var captionStyle = (Style)Application.Current.Resources["CaptionTextBlockStyle"];
        foreach (var tag in tags)
        {
            var content = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Children.Add(kind == "Runtime" && tag == "pt"
                ? new Image { Width = 20, Height = 20, Source = GetImage(SettingsIconFolder + "PowerToys.png") }
                : new FontIcon { FontSize = 20, Glyph = PickerGlyph(kind, tag) });
            content.Children.Add(new TextBlock
            {
                Style = captionStyle,
                Text = GetString($"PowerScripts_NewScript_{kind}_{tag}"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var item = new GridViewItem { Tag = tag, Content = content };
            AutomationProperties.SetAutomationId(item, $"PowerScriptsTemplate{kind}_{tag}");
            AutomationProperties.SetName(item, GetString(kind == "Runtime" ? $"PowerScripts_NewScript_Runtime_{tag}" : $"PowerScripts_NewScript_{kind}Name_{tag}"));
            picker.Items.Add(item);
        }
    }

    private void SelectPickerItem(GridView picker, string tag)
    {
        _updatingPickers = true;
        try
        {
            picker.SelectedItem = picker.Items.OfType<GridViewItem>().FirstOrDefault(item => (string)item.Tag == tag);
        }
        finally
        {
            _updatingPickers = false;
        }
    }

    private void BuildIconMenu()
    {
        foreach (var icon in PowerScriptTemplateIcon.All)
        {
            var item = new RadioMenuFlyoutItem
            {
                GroupName = "Icon",
                Text = icon.Name,
                Tag = icon,
                IsChecked = !icon.HasIcon,
                Icon = icon.HasIcon ? new ImageIcon { Source = GetImage(icon.ImageSource) } : new FontIcon { Glyph = DefaultGlyph },
            };
            AutomationProperties.SetAutomationId(item, icon.AutomationId);
            item.Click += IconPreset_Click;
            IconMenu.Items.Add(item);
        }

        IconMenu.Items.Add(new MenuFlyoutSeparator());
        var custom = new MenuFlyoutItem
        {
            Text = GetString("PowerScripts_NewScript_CustomIcon"),
            Icon = new FontIcon { Glyph = "\uE8E5" },
        };
        AutomationProperties.SetAutomationId(custom, "PowerScriptsTemplateCustomIcon");
        custom.Click += CustomIcon_Click;
        IconMenu.Items.Add(custom);
    }

    private void ShowTileGlyph()
    {
        TileImage.Source = null;
        TileImage.Visibility = Visibility.Collapsed;
        TileGlyph.Visibility = Visibility.Visible;
    }

    private void ShowTileImage(BitmapImage image)
    {
        TileImage.Source = image;
        TileImage.Visibility = Visibility.Visible;
        TileGlyph.Visibility = Visibility.Collapsed;
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        TemplateStatus.Severity = severity;
        TemplateStatus.Message = message;
        TemplateStatus.IsOpen = true;
    }

    private void QueueUpdate()
    {
        if (_updateQueued)
        {
            return;
        }

        _updateQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _updateQueued = false;
            if (_isActive)
            {
                UpdateOptions();
            }
        });
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(PowerScriptsViewModel.IsEnabled) or
            nameof(PowerScriptsViewModel.PythonModeIndex) or nameof(PowerScriptsViewModel.ScriptsFolder))
        {
            QueueUpdate();
        }
    }

    private void Consumer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(PowerScriptsViewModel.PowerScriptConsumerItem.IsEnabled))
        {
            QueueUpdate();
        }
    }

    private void UpdateOptions()
    {
        if (ViewModel is null)
        {
            return;
        }

        var cliMode = IsCliContent;
        CliPanel.Visibility = BoolToVisibility(cliMode);
        ParametersCard.Visibility = InvertBoolToVisibility(cliMode);
        PythonOffNote.IsOpen = _runtime == "python" && ViewModel.PythonModeIndex == 0;
        CliModuleComboBox.IsEnabled = !_loadingCliContent && CliModuleComboBox.Items.Count > 0;
        CliContentComboBox.IsEnabled = !_loadingCliContent && CliContentComboBox.Items.Count > 0;
        if (cliMode)
        {
            UpdateCliArguments();
            SyncCliState();
        }
        else
        {
            SetPickerItemsEnabled(InputPicker, _ => true);
            SetPickerItemsEnabled(OutputPicker, _ => true);
        }

        OptionsCard.Visibility = BoolToVisibility(cliMode && CliArguments.Count > 0);
        FilesOptions.Visibility = BoolToVisibility(!cliMode && _input == "files");
        UpdateGlyphs();
        UpdateInputDescription();
        UpdatePlaces();
        UpdateAsks();
        UpdateCreates();
        UpdateCreateButton();
    }

    private void SyncCliState()
    {
        var cli = SelectedCliContent;
        if (cli is null)
        {
            return;
        }

        var possible = PossibleCliInputs(cli).ToArray();
        _syncingCliSources = true;
        try
        {
            foreach (var argument in CliArguments)
            {
                argument.CanUseInput = possible.Contains(argument.InputType);
                if (argument.IsInput && !argument.CanUseInput)
                {
                    argument.SourceIndex = 0;
                }
            }
        }
        finally
        {
            _syncingCliSources = false;
        }

        _input = CliInput;
        _output = cli.Output;
        SetPickerItemsEnabled(InputPicker, tag => possible.Contains(tag));
        SetPickerItemsEnabled(OutputPicker, tag => tag == cli.Output);
        SelectPickerItem(InputPicker, _input);
        SelectPickerItem(OutputPicker, _output);
    }

    private void ApplyCliInput(string tag)
    {
        _syncingCliSources = true;
        try
        {
            foreach (var argument in CliArguments.Where(argument => argument.IsInput && argument.InputType != tag).ToList())
            {
                argument.SourceIndex = 0;
            }

            if (tag != "none" && !CliArguments.Any(argument => argument.IsInput))
            {
                var first = CliArguments.FirstOrDefault(argument => argument.CanUseInput && argument.InputType == tag);
                if (first is not null)
                {
                    first.SourceIndex = 1;
                }
            }
        }
        finally
        {
            _syncingCliSources = false;
        }
    }

    private void UpdateGlyphs()
    {
        TakesGlyph.Glyph = PickerGlyph("Input", _input);
        GivesGlyph.Glyph = PickerGlyph("Output", _output);
        if (IsCliContent)
        {
            var icon = SelectedCliContent?.IconName;
            RunsImage.Source = GetImage(SettingsIconFolder + (string.IsNullOrWhiteSpace(icon) ? "PowerToys" : icon) + ".png");
            RunsImage.Visibility = Visibility.Visible;
            RunsGlyph.Visibility = Visibility.Collapsed;
        }
        else
        {
            RunsGlyph.Glyph = PickerGlyph("Runtime", _runtime);
            RunsImage.Visibility = Visibility.Collapsed;
            RunsGlyph.Visibility = Visibility.Visible;
        }
    }

    private void UpdateInputDescription()
    {
        var text = GetString("PowerScripts_NewScript_InputHelp_" + _input);
        if (IsCliContent)
        {
            var labels = CliArguments.Where(argument => argument.IsInput).Select(argument => argument.Value.Label).ToArray();
            if (labels.Length > 0)
            {
                text += " " + Format(labels.Length == 1 ? "PowerScripts_NewScript_InputFills" : "PowerScripts_NewScript_InputFillsMany", string.Join(", ", labels));
            }
        }

        InputDescription.Text = text;
    }

    private void UpdatePlaces()
    {
        var available = new List<PowerScriptPreviewRow>();
        var unavailable = new List<PowerScriptPreviewRow>();

        void AddPlace(string id, string icon, PowerScriptsViewModel.PowerScriptConsumerItem consumer, bool eligible, string detail, string whyKey)
        {
            var rows = eligible ? available : unavailable;
            rows.Add(new PowerScriptPreviewRow
            {
                Title = GetString("PowerScripts_NewScript_Place_" + id),
                Detail = eligible ? detail : GetString("PowerScripts_NewScript_" + whyKey),
                Icon = GetImage(SettingsIconFolder + icon + ".png"),
                IsOff = eligible && (!ViewModel.IsEnabled || !consumer.IsEnabled),
                HasDivider = rows.Count > 0,
            });
        }

        var hasInput = _input != "none";
        AddPlace(
            "AdvancedPaste",
            "AdvancedPaste",
            ViewModel.AdvancedPasteConsumer,
            hasInput && _output != "none",
            Format("PowerScripts_NewScript_PlaceDetail_AdvancedPaste", GetString("PowerScripts_NewScript_TakesWord_" + _input)),
            hasInput ? "PlaceWhy_AdvancedPasteOutput" : "PlaceWhy_AdvancedPaste");
        AddPlace(
            "KeyboardManager",
            "KeyboardManager",
            ViewModel.KeyboardManagerConsumer,
            !hasInput,
            GetString("PowerScripts_NewScript_PlaceDetail_KeyboardManager"),
            "PlaceWhy_KeyboardManager");
        AddPlace(
            "CommandPalette",
            "CmdPal",
            ViewModel.CommandPaletteConsumer,
            !hasInput && _output is "none" or "text",
            GetString("PowerScripts_NewScript_PlaceDetail_CommandPalette"),
            hasInput ? "PlaceWhy_CommandPalette" : "PlaceWhy_CommandPaletteOutput");
        AddPlace(
            "FileExplorer",
            "FileExplorerPreview",
            ViewModel.FileExplorerConsumer,
            _input is "files" or "image" or "audio",
            Format("PowerScripts_NewScript_PlaceDetail_FileExplorer", ExplorerExtensions()),
            "PlaceWhy_FileExplorer");

        ReplaceRows(Places, available);
        ReplaceRows(UnavailablePlaces, unavailable);
        PlacesEmpty.Visibility = BoolToVisibility(Places.Count == 0);
        UnavailableExpander.Visibility = BoolToVisibility(UnavailablePlaces.Count > 0);
    }

    private string ExplorerExtensions()
    {
        if (IsCliContent || _input != "files" || !TryParseFileExtensions(FileExtensionsTextBox.Text, out var extensions) || extensions.Contains("*"))
        {
            return string.Empty;
        }

        return string.Join(", ", extensions) + " ";
    }

    private void UpdateAsks()
    {
        var rows = new List<PowerScriptPreviewRow>();
        if (IsCliContent)
        {
            foreach (var argument in CliArguments.Where(argument => argument.IsAsk))
            {
                rows.Add(new PowerScriptPreviewRow
                {
                    Title = argument.Value.DisplayLabel,
                    Glyph = argument.TypeGlyph,
                    TileKind = argument.TypeKind,
                    HasDivider = rows.Count > 0,
                });
            }
        }
        else
        {
            foreach (var parameter in Parameters.Where(parameter => parameter.TypeIndex >= 0))
            {
                rows.Add(new PowerScriptPreviewRow
                {
                    Title = string.IsNullOrWhiteSpace(parameter.Title) ? parameter.DisplayName : parameter.Title.Trim(),
                    Detail = parameter.TypeLabel,
                    Glyph = parameter.TypeGlyph,
                    TileKind = parameter.TypeKind,
                    HasDivider = rows.Count > 0,
                });
            }
        }

        ReplaceRows(AskRows, rows);
        AskEmpty.Visibility = BoolToVisibility(AskRows.Count == 0);
    }

    private void UpdateCreates()
    {
        var name = ProjectNameTextBox.Text.Trim();
        var slug = new string(name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        CreatesPath.Text = Path.Combine(ViewModel.ScriptsFolder ?? string.Empty, slug.Length == 0 ? "untitled-script" : slug);

        var entry = !IsCliContent && _runtime == "python" ? "script.py" : "script.ps1";
        var files = new List<string> { entry, entry + ".tool.json", "README.md" };
        if (_iconPreset is not null || _customIconPath is not null)
        {
            files.Add("icon.ico");
        }

        CreatesFiles.Text = string.Join(Separator, files);
    }

    private bool AreFileCountsValid()
    {
        var minimum = MinimumFilesNumberBox.Value;
        var maximum = MaximumFilesNumberBox.Value;
        return IsWholeCount(minimum) && minimum >= 1 && IsWholeCount(maximum) && (maximum == 0 || maximum >= minimum);
    }

    private void UpdateCreateButton()
    {
        if (_validating || ViewModel is null)
        {
            return;
        }

        _validating = true;
        try
        {
            var cliMode = IsCliContent;
            var valid = true;

            string filesError = null;
            if (!cliMode && _input == "files")
            {
                if (!TryParseFileExtensions(FileExtensionsTextBox.Text, out _))
                {
                    filesError = GetString("PowerScripts_NewScript_ExtensionsInvalid");
                }
                else if (!AreFileCountsValid())
                {
                    filesError = GetString("PowerScripts_TemplateInvalidFileCount");
                }
            }

            FilesError.Text = filesError ?? string.Empty;
            FilesError.Visibility = BoolToVisibility(filesError is not null);
            valid &= filesError is null;

            if (!cliMode)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var parameter in Parameters)
                {
                    valid &= TryValidateParameter(parameter, names, out var error);
                    parameter.Error = error;
                }
            }

            string cliError = null;
            if (cliMode)
            {
                valid &= ValidateCliArguments(out cliError);
            }

            CliBindingValidation.Text = cliError ?? string.Empty;
            CliBindingValidation.Visibility = BoolToVisibility(!string.IsNullOrEmpty(cliError));

            var hasName = !string.IsNullOrWhiteSpace(ProjectNameTextBox.Text);
            NameError.Text = GetString("PowerScripts_NewScript_NameRequired");
            NameError.Visibility = BoolToVisibility(_nameTouched && !hasName);

            CreateButton.IsEnabled = hasName && valid && !_isCreating &&
                !(cliMode && (_loadingCliContent || SelectedCliContent is null));
        }
        finally
        {
            _validating = false;
        }
    }

    private async Task LoadCliContentAsync()
    {
        if (_loadingCliContent || !_isActive)
        {
            return;
        }

        _loadingCliContent = true;
        var loading = GetString("PowerScripts_CliCatalogueLoading");
        CliCatalogueStatus.Severity = InfoBarSeverity.Informational;
        CliCatalogueStatus.Message = loading;
        CliCatalogueStatus.IsOpen = true;
        CliCatalogueRetry.Visibility = Visibility.Collapsed;
        CliModuleComboBox.PlaceholderText = loading;
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
                throw new InvalidDataException(GetString("PowerScripts_TemplateCliEmpty"));
            }

            _updatingCliSelection = true;
            try
            {
                CliModuleComboBox.Items.Clear();
                foreach (var module in PowerScriptCliModule.FromTemplates(templates))
                {
                    var item = new ComboBoxItem { Content = module.Name, Tag = module };
                    AutomationProperties.SetAutomationId(item, "PowerScriptsTemplateCliModule_" + module.Id);
                    CliModuleComboBox.Items.Add(item);
                }

                CliModuleComboBox.SelectedIndex = CliModuleComboBox.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                _updatingCliSelection = false;
            }

            UpdateCliActions();
            CliCatalogueStatus.IsOpen = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to load PowerShell module CLI content.", ex);
            if (_isActive)
            {
                CliModuleComboBox.Items.Clear();
                CliContentComboBox.Items.Clear();
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
                CliModuleComboBox.PlaceholderText = _modulePlaceholder;
                UpdateOptions();
            }
        }
    }

    private void UpdateCliActions()
    {
        _updatingCliSelection = true;
        try
        {
            if ((CliContentComboBox.SelectedItem as ComboBoxItem)?.Tag is PowerScriptOperationTemplate previous)
            {
                _selectedCliActions[previous.ModuleId] = previous.Id;
            }

            CliContentComboBox.Items.Clear();
            var module = SelectedCliModule;
            if (module is null)
            {
                return;
            }

            _selectedCliActions.TryGetValue(module.Id, out var preferred);
            ComboBoxItem selected = null;
            foreach (var template in module.Templates)
            {
                var item = new ComboBoxItem { Content = template.Name, Tag = template };
                AutomationProperties.SetAutomationId(item, "PowerScriptsTemplateCliContent_" + template.Id);
                CliContentComboBox.Items.Add(item);
                if (template.Id == preferred)
                {
                    selected = item;
                }
            }

            CliContentComboBox.SelectedItem = selected ?? (CliContentComboBox.Items.Count > 0 ? CliContentComboBox.Items[0] : null);
        }
        finally
        {
            _updatingCliSelection = false;
        }
    }

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = _isCreating || EditorNavigation.GoBack(Frame);
    }

    private void Field_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ProjectNameTextBox) && ProjectNameTextBox.Text.Length > 0)
        {
            _nameTouched = true;
        }

        UpdateOptions();
    }

    private void FileCount_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateCreateButton();

    private void BodyGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= WideLayoutMinimumWidth;
        SideColumn.Width = new GridLength(wide ? SideColumnWidth : 0);
        BodyGrid.ColumnSpacing = wide ? BodyColumnSpacing : 0;
        Grid.SetRow(SidePanel, wide ? 0 : 1);
        Grid.SetColumn(SidePanel, wide ? 1 : 0);
    }

    private void InputPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPickers)
        {
            return;
        }

        if (InputPicker.SelectedItem is not GridViewItem { Tag: string tag })
        {
            SelectPickerItem(InputPicker, _input);
            return;
        }

        _input = tag;
        if (IsCliContent)
        {
            ApplyCliInput(tag);
        }

        UpdateOptions();
    }

    private void RuntimePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPickers)
        {
            return;
        }

        if (RuntimePicker.SelectedItem is not GridViewItem { Tag: string tag })
        {
            SelectPickerItem(RuntimePicker, _runtime);
            return;
        }

        _runtime = tag;
        UpdateOptions();
        if (IsCliContent && CliModuleComboBox.Items.Count == 0)
        {
            _ = LoadCliContentAsync();
        }
    }

    private void OutputPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPickers)
        {
            return;
        }

        if (OutputPicker.SelectedItem is not GridViewItem { Tag: string tag })
        {
            SelectPickerItem(OutputPicker, _output);
            return;
        }

        _output = tag;
        UpdateOptions();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => EditorNavigation.OpenGlobalSettings();

    private void CliModule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingCliSelection || _loadingCliContent)
        {
            return;
        }

        UpdateCliActions();
        UpdateOptions();
    }

    private void CliContent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingCliSelection)
        {
            return;
        }

        if ((CliContentComboBox.SelectedItem as ComboBoxItem)?.Tag is PowerScriptOperationTemplate template)
        {
            _selectedCliActions[template.ModuleId] = template.Id;
        }

        UpdateOptions();
    }

    private void CliCatalogueRetry_Click(object sender, RoutedEventArgs e) => _ = LoadCliContentAsync();

    private void AddParameter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
        {
            return;
        }

        var parameter = new PowerScriptTemplateParameter(++_nextParameterId)
        {
            TypeIndex = int.Parse(tag, CultureInfo.InvariantCulture),
        };
        parameter.PropertyChanged += Parameter_PropertyChanged;
        Parameters.Add(parameter);
        UpdateOptions();
        DispatcherQueue.TryEnqueue(() =>
            (ParametersList.ContainerFromItem(parameter) as FrameworkElement)?
                .FindDescendant<TextBox>(box => box.Name == "ParameterNameBox")?
                .Focus(FocusState.Programmatic));
    }

    private void RemoveParameter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PowerScriptTemplateParameter parameter })
        {
            return;
        }

        parameter.PropertyChanged -= Parameter_PropertyChanged;
        Parameters.Remove(parameter);
        UpdateOptions();
    }

    private void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PowerScriptTemplateParameter.Error) or nameof(PowerScriptTemplateParameter.HasError) or
            nameof(PowerScriptTemplateParameter.Meta))
        {
            return;
        }

        UpdateOptions();
    }

    private void IconPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem { Tag: PowerScriptTemplateIcon icon })
        {
            return;
        }

        _customIconPath = null;
        CustomIconPanel.Visibility = Visibility.Collapsed;
        _iconPreset = icon.HasIcon ? icon.Id : null;
        if (icon.HasIcon)
        {
            ShowTileImage(GetImage(icon.ImageSource));
        }
        else
        {
            ShowTileGlyph();
        }

        AutomationProperties.SetName(IconButton, Format("PowerScripts_NewScript_IconName", icon.Name));
        UpdateOptions();
    }

    private async void CustomIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreating)
        {
            return;
        }

        _isCreating = true;
        App.MainWindow.UpdateBackButton();
        UpdateCreateButton();
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.MainWindow.AppWindow.Id);
            picker.FileTypeFilter.Add(".ico");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var image = new BitmapImage();
            using (var stream = File.OpenRead(file.Path))
            {
                if (stream.Length > 1024 * 1024 || !string.Equals(Path.GetExtension(file.Path), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(GetString("PowerScripts_TemplateCustomIconInvalid"));
                }

                await image.SetSourceAsync(stream.AsRandomAccessStream());
            }

            _customIconPath = file.Path;
            _iconPreset = null;
            ShowTileImage(image);
            CustomIconName.Text = Path.GetFileName(file.Path);
            CustomIconPanel.Visibility = Visibility.Visible;
            foreach (var item in IconMenu.Items.OfType<RadioMenuFlyoutItem>())
            {
                item.IsChecked = false;
            }

            AutomationProperties.SetName(IconButton, Format("PowerScripts_NewScript_IconName", CustomIconName.Text));
            TemplateStatus.IsOpen = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to choose a custom PowerScript icon.", ex);
            ShowStatus(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            _isCreating = false;
            App.MainWindow.UpdateBackButton();
            UpdateOptions();
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreating || ViewModel is null)
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
            var cliMode = IsCliContent;
            var takesFiles = _input == "files";
            entry = await ViewModel.CreateTemplateAsync(
                ProjectNameTextBox.Text,
                DescriptionTextBox.Text,
                cliMode ? "powershell" : _runtime,
                _input,
                _output,
                !cliMode && takesFiles ? FileExtensionsTextBox.Text : null,
                takesFiles ? FileCount(MinimumFilesNumberBox) : 1,
                takesFiles ? FileCount(MaximumFilesNumberBox) : 0,
                cliMode ? new JsonArray() : new JsonArray(Parameters.Select(parameter => (JsonNode)parameter.ToTemplateParameter()).ToArray()),
                _iconPreset,
                _customIconPath,
                cliContent: SelectedCliContent?.Id,
                cliBindings: CreateCliBindings());
            ShowStatus(InfoBarSeverity.Success, GetString("PowerScripts_TemplateCreated") + Environment.NewLine + Path.GetDirectoryName(entry));
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to create configured PowerScript.", ex);
            ShowStatus(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            _isCreating = false;
            App.MainWindow.UpdateBackButton();
            CreationForm.IsEnabled = true;
            UpdateCreateButton();
        }

        if (entry is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{entry}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogError("PowerScript created, but its folder could not be opened.", ex);
            TemplateStatus.Severity = InfoBarSeverity.Warning;
            TemplateStatus.Message += Environment.NewLine + GetString("PowerScripts_TemplateFolderFailed") + " " + ex.Message;
        }
    }
}

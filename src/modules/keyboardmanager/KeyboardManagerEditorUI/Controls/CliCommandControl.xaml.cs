// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerScripts.Client;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace KeyboardManagerEditorUI.Controls
{
    /// <summary>Renders a dialog-local parameter draft from the Host's read-only CLI catalogue.</summary>
    public sealed partial class CliCommandControl : UserControl, IDisposable
    {
        private readonly ObservableCollection<CommandChoice> _commands = new();
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextBlock> _fieldErrors = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _touchedFields = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(DependencyObject Control, DependencyProperty Property, long Token)> _propertyCallbacks = new();
        private readonly Dictionary<string, CompositeFormat> _resourceFormats = new(StringComparer.Ordinal);

        private PowerScriptsClient? _client;
        private CancellationTokenSource? _catalogCancellation;
        private CancellationTokenSource? _testCancellation;
        private string? _requestedCommandId;
        private string? _catalogError;
        private int _catalogGeneration;
        private int _testGeneration;
        private bool _isActive;
        private bool _catalogLoaded;
        private bool _isLoading;
        private bool _isTesting;
        private bool _updatingControls;
        private bool _showValidation;

        /// <summary>Initializes the CLI command picker and its independent draft.</summary>
        public CliCommandControl()
        {
            InitializeComponent();
            CommandComboBox.ItemsSource = _commands;
        }

        /// <summary>Raised when discovery, parameters, or testing change save availability.</summary>
        public event EventHandler? ValidationStateChanged;

        /// <summary>Requests that the outer shortcut recorder release its keyboard hook.</summary>
        public event EventHandler? InputFocusRequested;

        /// <summary>Starts discovery only while the CLI action is being presented.</summary>
        public void Activate()
        {
            _isActive = true;
            if (IsLoaded && !_catalogLoaded && !_isLoading)
            {
                _ = LoadCatalogAsync();
            }
        }

        /// <summary>Stops outstanding work while preserving the current dialog's draft.</summary>
        public void Deactivate()
        {
            _isActive = false;
            CancelCatalog();
            CancelTest();
            UpdateState();
        }

        /// <summary>Releases asynchronous work and parameter callbacks when the editor is disposed.</summary>
        public void Dispose()
        {
            Deactivate();
            ClearParameterControls();
            GC.SuppressFinalize(this);
        }

        /// <summary>Clears the draft and catalogue so another dialog cannot inherit their state.</summary>
        public void Reset()
        {
            Deactivate();
            _catalogLoaded = false;
            _catalogError = null;
            _requestedCommandId = null;
            _client = null;
            _values.Clear();
            _touchedFields.Clear();
            _showValidation = false;
            _updatingControls = true;
            CommandComboBox.SelectedItem = null;
            _commands.Clear();
            _updatingControls = false;
            ClearParameterControls();
            ClearTestResult();
            UpdateState();
        }

        /// <summary>Gets the selected catalogue entry, or null for a missing saved command.</summary>
        public CliCommandInfo? GetSelectedCommand() => SelectedChoice?.Command;

        /// <summary>Returns a copy of the effective values, including defaults shown in the form.</summary>
        public IReadOnlyDictionary<string, string?> GetParameters()
        {
            if (GetSelectedCommand() is { } command &&
                CliParameterValues.TryValidate(command.Parameters, _values, out var normalized, out _, out _))
            {
                return new Dictionary<string, string?>(normalized, StringComparer.Ordinal);
            }

            return new Dictionary<string, string?>(_values, StringComparer.Ordinal);
        }

        /// <summary>Retains saved values even if discovery has not completed or the command was removed.</summary>
        public void SetBinding(string commandId, IReadOnlyDictionary<string, string?> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            CancelTest();
            ClearTestResult();
            _requestedCommandId = commandId;
            _values.Clear();
            foreach (var (name, value) in parameters)
            {
                _values[name] = value;
            }

            _touchedFields.Clear();
            _showValidation = false;
            SelectRequestedCommand();
            RenderParameters();
            UpdateState();
        }

        /// <summary>Gets the reason this draft cannot be saved, independently of its trigger.</summary>
        public string? GetValidationError()
        {
            if (_isLoading)
            {
                return ResourceHelper.GetString("CliCatalogLoading");
            }

            if (!_catalogLoaded)
            {
                return _catalogError ?? ResourceHelper.GetString("CliCatalogUnavailable");
            }

            if (SelectedChoice is not { } choice)
            {
                return ResourceHelper.GetString("CliSelectCommand");
            }

            if (choice.Command is not { } command)
            {
                return ResourceHelper.GetString("CliSavedCommandMissing");
            }

            if (!command.IsAvailable)
            {
                return string.IsNullOrWhiteSpace(command.UnavailableReason)
                    ? ResourceHelper.GetString("CliCommandUnavailable")
                    : command.UnavailableReason;
            }

            if (_isTesting)
            {
                return ResourceHelper.GetString("CliTestRunning");
            }

            return CliParameterValues.TryValidate(command.Parameters, _values, out _, out var errorKey, out var name)
                ? null
                : GetParameterError(errorKey, name);
        }

        private CommandChoice? SelectedChoice => CommandComboBox.SelectedItem as CommandChoice;

        private void CliCommandControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isActive)
            {
                Activate();
            }
        }

        private void CliCommandControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Deactivate();
        }

        private void CliInput_GotFocus(object sender, RoutedEventArgs e)
        {
            InputFocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void CatalogRetryButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCatalogAsync();
        }

        private async Task LoadCatalogAsync()
        {
            CancelCatalog();
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var generation = ++_catalogGeneration;
            _catalogCancellation = cancellation;
            _isLoading = true;
            _catalogError = null;
            UpdateState();

            try
            {
                // Discovery also probes the installation path; do that work off the UI thread.
                var (client, result) = await Task.Run(
                    async () =>
                    {
                        var discoveredClient = new PowerScriptsClient();
                        var catalogue = await discoveredClient.GetCliCommandsAsync(token).ConfigureAwait(false);
                        return (discoveredClient, catalogue);
                    },
                    token);

                if (generation != _catalogGeneration || !_isActive || token.IsCancellationRequested)
                {
                    return;
                }

                _client = client;
                _catalogLoaded = result.Succeeded;
                if (!result.Succeeded)
                {
                    _catalogError = string.IsNullOrWhiteSpace(result.Error)
                        ? ResourceHelper.GetString("CliCatalogUnavailable")
                        : result.Error;
                    return;
                }

                _requestedCommandId ??= SelectedChoice?.Id;
                _updatingControls = true;
                _commands.Clear();
                foreach (var command in result.Commands
                    .OrderBy(command => command.Group, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(command => command.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    _commands.Add(new CommandChoice(command.Id, command.Name, command.Group, command));
                }

                _updatingControls = false;
                SelectRequestedCommand();
                RenderParameters();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Closing or resetting a dialog must not publish a stale catalogue into the next one.
            }
            catch (Exception)
            {
                if (generation == _catalogGeneration)
                {
                    _catalogLoaded = false;
                    _catalogError = ResourceHelper.GetString("CliCatalogUnavailable");
                }
            }
            finally
            {
                _updatingControls = false;
                if (generation == _catalogGeneration)
                {
                    _catalogCancellation = null;
                    _isLoading = false;
                    UpdateState();
                }

                cancellation.Dispose();
            }
        }

        private void SelectRequestedCommand()
        {
            _updatingControls = true;
            var choice = _commands.FirstOrDefault(command => string.Equals(command.Id, _requestedCommandId, StringComparison.Ordinal));
            if (choice is null && !string.IsNullOrEmpty(_requestedCommandId))
            {
                choice = new CommandChoice(_requestedCommandId, _requestedCommandId, string.Empty, null);
                _commands.Add(choice);
            }

            CommandComboBox.SelectedItem = choice;
            _updatingControls = false;
        }

        private void CommandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingControls)
            {
                return;
            }

            CancelTest();
            ClearTestResult();
            _requestedCommandId = SelectedChoice?.Id;
            _values.Clear();
            _touchedFields.Clear();
            _showValidation = false;
            RenderParameters();
            UpdateState();
        }

        private void ResetParametersButton_Click(object sender, RoutedEventArgs e)
        {
            _values.Clear();
            _touchedFields.Clear();
            _showValidation = false;
            ClearTestResult();
            RenderParameters();
            UpdateState();
        }

        private void RenderParameters()
        {
            ClearParameterControls();
            _updatingControls = true;
            var command = GetSelectedCommand();
            if (command is not null)
            {
                foreach (var parameter in command.Parameters)
                {
                    if (!TryGetValue(parameter.Name, out _) && parameter.Default is not null)
                    {
                        _values[parameter.Name] = parameter.Default;
                    }

                    // An unchecked checkbox is an explicit false value in the form.
                    if (parameter.Type.Equals("bool", StringComparison.OrdinalIgnoreCase) && !TryGetValue(parameter.Name, out _))
                    {
                        _values[parameter.Name] = "false";
                    }

                    AddParameterField(parameter);
                }
            }

            // Preserve obsolete or temporarily undiscoverable saved values rather than silently dropping them.
            foreach (var value in _values.Where(value => command is null || !command.Parameters.Any(
                parameter => parameter.Name.Equals(value.Key, StringComparison.OrdinalIgnoreCase))))
            {
                var field = new StackPanel { Spacing = 4 };
                field.Children.Add(new TextBlock { Text = value.Key, TextWrapping = TextWrapping.Wrap });
                field.Children.Add(new TextBox { Text = value.Value ?? string.Empty, IsReadOnly = true });
                ParametersPanel.Children.Add(field);
            }

            _updatingControls = false;
        }

        private void AddParameterField(PowerScriptParameter parameter)
        {
            TryGetValue(parameter.Name, out var value);
            var field = new StackPanel { Spacing = 4 };
            var label = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            if (parameter.IsRequired)
            {
                label = string.Format(CultureInfo.CurrentCulture, GetResourceFormat("CliRequiredParameterLabel"), label);
            }

            var labelText = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
            var editor = CreateParameterEditor(parameter, value, label);
            if (editor is not CheckBox)
            {
                field.Children.Add(labelText);
                AutomationProperties.SetLabeledBy(editor, labelText);
            }

            AutomationProperties.SetName(editor, label);
            AutomationProperties.SetAutomationId(editor, "CliParameter_" + parameter.Name);
            editor.LostFocus += (_, _) =>
            {
                _touchedFields.Add(parameter.Name);
                UpdateState();
            };
            field.Children.Add(editor);

            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                field.Children.Add(CreateCaption(parameter.Description));
                AutomationProperties.SetHelpText(editor, parameter.Description);
            }

            if (parameter.Min is { } min)
            {
                field.Children.Add(CreateCaption(string.Format(CultureInfo.CurrentCulture, GetResourceFormat("CliParameterMinimum"), min)));
            }

            if (parameter.Max is { } max)
            {
                field.Children.Add(CreateCaption(string.Format(CultureInfo.CurrentCulture, GetResourceFormat("CliParameterMaximum"), max)));
            }

            var errorText = CreateCaption(string.Empty);
            errorText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            errorText.Visibility = Visibility.Collapsed;
            field.Children.Add(errorText);
            _fieldErrors[parameter.Name] = errorText;
            ParametersPanel.Children.Add(field);
        }

        private FrameworkElement CreateParameterEditor(PowerScriptParameter parameter, string? value, string label)
        {
            switch (parameter.Type.ToLowerInvariant())
            {
                case "bool":
                    var boolean = new CheckBox { Content = label, IsChecked = bool.TryParse(value, out var parsedBoolean) && parsedBoolean };
                    boolean.Checked += (_, _) => SetParameterValue(parameter.Name, "true");
                    boolean.Unchecked += (_, _) => SetParameterValue(parameter.Name, "false");
                    return boolean;

                case "int":
                    var number = new NumberBox
                    {
                        SmallChange = 1,
                        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                        ValidationMode = NumberBoxValidationMode.Disabled,
                        Value = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber) ? parsedNumber : double.NaN,
                    };
                    if (!string.IsNullOrEmpty(value) && double.IsNaN(number.Value))
                    {
                        number.Text = value;
                    }

                    var token = number.RegisterPropertyChangedCallback(NumberBox.TextProperty, (_, _) =>
                    {
                        var text = number.Text;
                        var normalizedText = int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var integer)
                            ? integer.ToString(CultureInfo.InvariantCulture)
                            : text;
                        SetParameterValue(parameter.Name, normalizedText);
                    });
                    _propertyCallbacks.Add((number, NumberBox.TextProperty, token));
                    return number;

                case "choice":
                    var choice = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
                    foreach (var option in parameter.Options)
                    {
                        choice.Items.Add(option);
                    }

                    choice.SelectedItem = parameter.Options.FirstOrDefault(option => string.Equals(option, value, StringComparison.Ordinal));
                    choice.SelectionChanged += (_, _) => SetParameterValue(parameter.Name, choice.SelectedItem as string);
                    return choice;

                case "file":
                    var fileRow = new Grid { ColumnSpacing = 4 };
                    fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var path = new TextBox { Text = value ?? string.Empty };
                    path.TextChanged += (_, _) => SetParameterValue(parameter.Name, path.Text);
                    var browse = new Button { Content = ResourceHelper.GetString("CliBrowseFile") };
                    AutomationProperties.SetName(browse, string.Format(CultureInfo.CurrentCulture, GetResourceFormat("CliBrowseParameterFile"), label));
                    browse.Click += async (_, _) => await BrowseFileAsync(parameter.Name, path);
                    Grid.SetColumn(browse, 1);
                    fileRow.Children.Add(path);
                    fileRow.Children.Add(browse);
                    AutomationProperties.SetName(path, label);
                    return fileRow;

                case "string":
                    var textBox = new TextBox { Text = value ?? string.Empty };
                    textBox.TextChanged += (_, _) => SetParameterValue(parameter.Name, textBox.Text);
                    return textBox;

                default:
                    return new TextBox { Text = value ?? string.Empty, IsReadOnly = true };
            }
        }

        private async Task BrowseFileAsync(string parameterName, TextBox path)
        {
            var generation = _catalogGeneration;
            var commandId = SelectedChoice?.Id;
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
                var file = await picker.PickSingleFileAsync();
                if (file is not null && _isActive && generation == _catalogGeneration && commandId == SelectedChoice?.Id)
                {
                    path.Text = file.Path;
                    _touchedFields.Add(parameterName);
                }
            }
            catch (Exception)
            {
                if (_isActive && generation == _catalogGeneration)
                {
                    ShowTestResult(InfoBarSeverity.Error, ResourceHelper.GetString("CliFilePickerFailed"), string.Empty);
                }
            }
        }

        private void SetParameterValue(string name, string? value)
        {
            if (_updatingControls)
            {
                return;
            }

            foreach (var key in _values.Keys.Where(key => key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _values.Remove(key);
            }

            _values[name] = value;
            ClearTestResult();
            UpdateState();
        }

        private bool TryGetValue(string name, out string? value)
        {
            foreach (var item in _values)
            {
                if (item.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private async void TestCommandButton_Click(object sender, RoutedEventArgs e)
        {
            _showValidation = true;
            UpdateState();
            if (GetValidationError() is { } error)
            {
                ShowTestResult(InfoBarSeverity.Error, ResourceHelper.GetString("CliTestCannotRun"), error);
                return;
            }

            var command = GetSelectedCommand();
            var client = _client;
            if (command is null || client is null)
            {
                return;
            }

            var values = GetParameters();
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var generation = ++_testGeneration;
            _testCancellation = cancellation;
            _isTesting = true;
            ClearTestResult();
            UpdateState();
            try
            {
                var result = await Task.Run(() => client.RunCliAsync(command.Id, values, cancellationToken: token), token);
                if (generation != _testGeneration || !_isActive || token.IsCancellationRequested)
                {
                    return;
                }

                var title = ResourceHelper.GetString(result.Succeeded ? "CliTestSucceeded" : "CliTestFailed");
                var message = result.Succeeded
                    ? ResourceHelper.GetString("CliTestCompleted")
                    : result.Error ?? string.Format(CultureInfo.CurrentCulture, GetResourceFormat("CliTestFailedStatus"), result.Status, result.ExitCode);
                ShowTestResult(result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error, title, message);
                TestOutputTextBox.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    GetResourceFormat("CliTestOutputFormat"),
                    result.ExitCode,
                    LimitOutput(result.StdOut),
                    LimitOutput(result.StdErr));
                TestOutputExpander.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Deactivate/Reset deliberately discard results from the previous draft.
            }
            catch (Exception)
            {
                if (generation == _testGeneration && _isActive)
                {
                    ShowTestResult(InfoBarSeverity.Error, ResourceHelper.GetString("CliTestFailed"), ResourceHelper.GetString("CliTestUnexpectedError"));
                }
            }
            finally
            {
                if (generation == _testGeneration)
                {
                    _testCancellation = null;
                    _isTesting = false;
                    UpdateState();
                }

                cancellation.Dispose();
            }
        }

        private void CancelTestButton_Click(object sender, RoutedEventArgs e)
        {
            CancelTest();
            ShowTestResult(InfoBarSeverity.Informational, ResourceHelper.GetString("CliTestCancelled"), string.Empty);
            UpdateState();
        }

        private void CancelCatalog()
        {
            ++_catalogGeneration;
            _catalogCancellation?.Cancel();
            _catalogCancellation = null;
            _isLoading = false;
        }

        private void CancelTest()
        {
            ++_testGeneration;
            _testCancellation?.Cancel();
            _testCancellation = null;
            _isTesting = false;
        }

        private void UpdateState()
        {
            CatalogProgressRing.IsActive = _isLoading;
            CatalogProgressRing.Visibility = _isLoading ? Visibility.Visible : Visibility.Collapsed;
            CatalogInfoBar.Title = ResourceHelper.GetString("CliCatalogLoadFailed");
            CatalogInfoBar.Message = _catalogError ?? string.Empty;
            CatalogInfoBar.IsOpen = _catalogError is not null;
            CatalogRetryButton.Visibility = _catalogError is not null ? Visibility.Visible : Visibility.Collapsed;
            CatalogRetryButton.IsEnabled = !_isLoading && !_isTesting;
            CommandComboBox.IsEnabled = _catalogLoaded && !_isLoading && !_isTesting;

            var command = GetSelectedCommand();
            CommandDescriptionText.Text = command?.Description ?? string.Empty;
            CommandDescriptionText.Visibility = string.IsNullOrWhiteSpace(command?.Description) ? Visibility.Collapsed : Visibility.Visible;
            ParametersContainer.IsEnabled = command?.IsAvailable == true && !_isLoading && !_isTesting;
            NoParametersText.Visibility = command is not null && command.Parameters.Count == 0 && _values.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ResetParametersButton.Visibility = command is not null && (command.Parameters.Count > 0 || _values.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            ResetParametersButton.IsEnabled = command?.IsAvailable == true && !_isLoading && !_isTesting;

            string? commandError = null;
            foreach (var field in _fieldErrors.Values)
            {
                field.Visibility = Visibility.Collapsed;
            }

            if (_catalogLoaded && !_isLoading && SelectedChoice is not null && command is null)
            {
                commandError = ResourceHelper.GetString("CliSavedCommandMissing");
            }
            else if (command is not null)
            {
                if (!command.IsAvailable)
                {
                    commandError = string.IsNullOrWhiteSpace(command.UnavailableReason)
                        ? ResourceHelper.GetString("CliCommandUnavailable")
                        : command.UnavailableReason;
                }
                else if (!CliParameterValues.TryValidate(command.Parameters, _values, out _, out var key, out var name))
                {
                    var message = GetParameterError(key, name);
                    if (key is "CliParameterUnsupportedType" or "CliParameterUnknown")
                    {
                        commandError = message;
                    }

                    if (name is not null && _fieldErrors.TryGetValue(name, out var errorField) && (_showValidation || _touchedFields.Contains(name)))
                    {
                        errorField.Text = message;
                        errorField.Visibility = Visibility.Visible;
                    }
                }
            }

            CommandInfoBar.Title = ResourceHelper.GetString("CliCommandNeedsAttention");
            CommandInfoBar.Message = commandError ?? string.Empty;
            CommandInfoBar.IsOpen = commandError is not null;
            TestCommandButton.IsEnabled = _catalogLoaded && command?.IsAvailable == true && !_isLoading && !_isTesting;
            TestProgressRing.IsActive = _isTesting;
            TestProgressRing.Visibility = _isTesting ? Visibility.Visible : Visibility.Collapsed;
            CancelTestButton.Visibility = _isTesting ? Visibility.Visible : Visibility.Collapsed;
            ValidationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private string GetParameterError(string? resourceKey, string? parameterName)
        {
            var parameter = GetSelectedCommand()?.Parameters.FirstOrDefault(parameter => parameter.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
            var label = parameter is not null && !string.IsNullOrWhiteSpace(parameter.Label)
                ? parameter.Label
                : parameterName ?? string.Empty;
            return string.Format(CultureInfo.CurrentCulture, GetResourceFormat(resourceKey ?? "CliParameterUnknown"), label);
        }

        private CompositeFormat GetResourceFormat(string resourceKey)
        {
            if (!_resourceFormats.TryGetValue(resourceKey, out var format))
            {
                format = CompositeFormat.Parse(ResourceHelper.GetString(resourceKey));
                _resourceFormats.Add(resourceKey, format);
            }

            return format;
        }

        private void ClearParameterControls()
        {
            foreach (var (control, property, token) in _propertyCallbacks)
            {
                control.UnregisterPropertyChangedCallback(property, token);
            }

            _propertyCallbacks.Clear();
            _fieldErrors.Clear();
            ParametersPanel.Children.Clear();
        }

        private void ClearTestResult()
        {
            TestResultInfoBar.IsOpen = false;
            TestOutputExpander.IsExpanded = false;
            TestOutputExpander.Visibility = Visibility.Collapsed;
            TestOutputTextBox.Text = string.Empty;
        }

        private void ShowTestResult(InfoBarSeverity severity, string title, string message)
        {
            TestResultInfoBar.Severity = severity;
            TestResultInfoBar.Title = title;
            TestResultInfoBar.Message = message;
            TestResultInfoBar.IsOpen = true;
        }

        private static TextBlock CreateCaption(string value) => new()
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        private static string LimitOutput(string value) => value.Length <= 6000
            ? value
            : value[..6000] + Environment.NewLine + ResourceHelper.GetString("CliTestOutputTruncated");

        private sealed record CommandChoice(string Id, string DisplayName, string Group, CliCommandInfo? Command);
    }
}

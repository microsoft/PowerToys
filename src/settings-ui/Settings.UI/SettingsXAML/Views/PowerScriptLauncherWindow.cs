// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerScripts.Client;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed class PowerScriptLauncherWindow : WindowEx
{
    private static readonly CompositeFormat ParameterLabelFormat = CompositeFormat.Parse(Text("ParameterLabel"));
    private static readonly CompositeFormat DefaultFormat = CompositeFormat.Parse(Text("Default"));
    private static readonly CompositeFormat MissingParameterFormat = CompositeFormat.Parse(Text("MissingParameter"));
    private static readonly CompositeFormat FailureFormat = CompositeFormat.Parse(Text("Failure"));
    private static readonly CompositeFormat SavedFormat = CompositeFormat.Parse(Text("Saved"));
    private readonly PowerScriptsClient _client = new();
    private readonly Grid _root = new() { Padding = new Thickness(24), RowSpacing = 16 };
    private readonly TextBlock _heading = new() { TextWrapping = TextWrapping.Wrap };
    private readonly InfoBar _status = new() { IsClosable = false };
    private readonly ContentControl _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Button _back = Button("Back", "Back");
    private readonly Button _refresh = Button("Refresh", "Refresh");
    private readonly Button _run = Button("Run", "Run");
    private readonly Button _cancel = Button("Cancel", "Cancel");
    private readonly ProgressRing _progress = new() { Width = 24, Height = 24 };
    private readonly AutoSuggestBox _search = Id(new AutoSuggestBox { QueryIcon = new SymbolIcon(Symbol.Find), PlaceholderText = Text("Search") }, "Search");
    private readonly ListView _list = Id(new ListView { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single }, "Scripts");
    private readonly TextBlock _empty = Id(new TextBlock { Text = Text("Empty"), TextWrapping = TextWrapping.Wrap }, "Empty");
    private readonly Dictionary<string, Func<string>> _parameterValues = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PowerScriptInfo> _scripts = Array.Empty<PowerScriptInfo>();
    private PowerScriptInfo _selected;
    private FrameworkElement _catalogue;
    private FrameworkElement _parameterForm;
    private CancellationTokenSource _operation;
    private bool _busy;
    private bool _closed;
    private bool _cancelRequested;
    private bool _catalogueVisible;

    public PowerScriptLauncherWindow()
    {
        Title = Text("Title");
        IsMaximizable = false;
        IsMinimizable = false;
        SystemBackdrop = new MicaBackdrop();
        _root.RequestedTheme = App.ThemeService.Theme;
        App.ThemeService.ThemeChanged += ThemeChanged;
        AutomationProperties.SetAutomationId(_root, "PowerScriptsLauncher");
        AutomationProperties.SetAutomationId(_heading, "PowerScriptsLauncher_Heading");
        AutomationProperties.SetAutomationId(_status, "PowerScriptsLauncher_Status");
        _heading.Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"];
        _run.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        for (var index = 0; index < 4; index++)
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = index == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        }

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(_heading);
        header.Children.Add(new TextBlock { Text = Text("Intro"), TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        _root.Children.Add(header);
        Grid.SetRow(_status, 1);
        _root.Children.Add(_status);
        Grid.SetRow(_body, 2);
        _root.Children.Add(_body);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        footer.Children.Add(_back);
        footer.Children.Add(_refresh);
        footer.Children.Add(_run);
        footer.Children.Add(_cancel);
        footer.Children.Add(_progress);
        Grid.SetRow(footer, 3);
        _root.Children.Add(footer);
        Content = _root;
        _back.Click += async (_, _) =>
        {
            if (!_catalogueVisible && _parameterForm is not null && !ReferenceEquals(_body.Content, _parameterForm))
            {
                ShowParameters();
            }
            else
            {
                await ShowCatalogueAsync();
            }
        };
        _refresh.Click += async (_, _) => await ShowCatalogueAsync();
        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => CancelOperation();
        _search.TextChanged += (_, _) => FilterScripts();
        _search.QuerySubmitted += async (_, _) =>
        {
            if (_list.SelectedItem is ListViewItem { Tag: PowerScriptInfo script })
            {
                await SelectScriptAsync(script);
            }
        };
        _search.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Down && _list.Items.Count > 0)
            {
                _list.Focus(FocusState.Keyboard);
                args.Handled = true;
            }
        };
        _list.ItemClick += async (_, args) =>
        {
            if (args.ClickedItem is FrameworkElement { Tag: PowerScriptInfo script })
            {
                await SelectScriptAsync(script);
            }
        };
        _root.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Escape)
            {
                if (_busy)
                {
                    CancelOperation();
                }
                else
                {
                    AppWindow.Hide();
                }

                args.Handled = true;
            }
        };
        Closed += (_, _) =>
        {
            _closed = true;
            _operation?.Cancel();
            App.ThemeService.ThemeChanged -= ThemeChanged;
        };

        var owner = App.GetSettingsWindow();
        var display = DisplayArea.GetFromWindowId(owner.AppWindow.Id, DisplayAreaFallback.Nearest);
        var scale = owner.Content?.XamlRoot?.RasterizationScale ?? 1;
        var width = Math.Min((int)(760 * scale), display.WorkArea.Width - 32);
        var height = Math.Min((int)(600 * scale), display.WorkArea.Height - 32);
        AppWindow.MoveAndResize(new RectInt32(
            display.WorkArea.X + ((display.WorkArea.Width - width) / 2),
            display.WorkArea.Y + ((display.WorkArea.Height - height) / 2),
            width,
            height));
        ShowCatalogueSurface();
    }

    public async void ShowLauncher()
    {
        Activate();
        WindowHelpers.BringToForeground(WindowNative.GetWindowHandle(this));
        if (_catalogueVisible && !_busy)
        {
            await ShowCatalogueAsync();
        }
    }

    private static string Text(string key) => ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_Launcher" + key);

    private static T Id<T>(T element, string id)
        where T : DependencyObject
    {
        AutomationProperties.SetAutomationId(element, "PowerScriptsLauncher_" + id);
        return element;
    }

    private static Button Button(string label, string id) => Id(new Button { Content = Text(label) }, id);

    private void ThemeChanged(object sender, ElementTheme theme) => _root.RequestedTheme = theme;

    private void ShowCatalogueSurface()
    {
        _catalogueVisible = true;
        _heading.Text = Text("Heading");
        _back.Visibility = Visibility.Collapsed;
        _run.Visibility = Visibility.Collapsed;
        _refresh.Visibility = Visibility.Visible;
        if (_catalogue is null)
        {
            var catalogue = new Grid { RowSpacing = 12 };
            catalogue.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            catalogue.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            catalogue.Children.Add(_search);
            Grid.SetRow(_list, 1);
            Grid.SetRow(_empty, 1);
            catalogue.Children.Add(_list);
            catalogue.Children.Add(_empty);
            _catalogue = catalogue;
        }

        _body.Content = _catalogue;
    }

    private async Task ShowCatalogueAsync()
    {
        if (_busy)
        {
            return;
        }

        _selected = null;
        _parameterForm = null;
        _parameterValues.Clear();
        ShowCatalogueSurface();
        _scripts = Array.Empty<PowerScriptInfo>();
        FilterScripts();
        SetBusy(true);
        _status.IsOpen = false;
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _operation = operation;
        try
        {
            _scripts = await _client.ListAsync(operation.Token);
            if (!_closed)
            {
                FilterScripts();
                _search.Focus(FocusState.Programmatic);
            }
        }
        catch (OperationCanceledException)
        {
            if (!_closed)
            {
                ShowMessage(Text(_cancelRequested ? "Cancelled" : "DiscoveryTimeout"), InfoBarSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            _operation = null;
            if (!_closed)
            {
                SetBusy(false);
            }
        }
    }

    private void FilterScripts()
    {
        _list.Items.Clear();
        var scripts = PowerScriptLauncherModel.FilterScripts(_scripts, _search.Text);
        foreach (var script in scripts)
        {
            var content = new StackPanel { Tag = script, Spacing = 4, Padding = new Thickness(8) };
            content.Children.Add(new TextBlock { Text = script.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = script.Description, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock
            {
                Text = $"{script.Runtime} / {Text("Output" + script.Output)} / {script.Parameters.Count} {Text("Parameters")}" +
                    (script.Trusted ? string.Empty : " / " + Text("Approval")),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap,
            });
            var item = Id(new ListViewItem { Content = content, Tag = script, HorizontalContentAlignment = HorizontalAlignment.Stretch }, "Script_" + script.Id);
            AutomationProperties.SetName(item, script.Name);
            _list.Items.Add(item);
        }

        _empty.Visibility = scripts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    private async Task SelectScriptAsync(PowerScriptInfo script)
    {
        if (_busy)
        {
            return;
        }

        _selected = script;
        _catalogueVisible = false;
        _parameterValues.Clear();
        _status.IsOpen = false;
        _parameterForm = null;
        if (script.Parameters.Count == 0)
        {
            await RunAsync();
            return;
        }

        var fields = new StackPanel { Spacing = 16 };
        foreach (var parameter in script.Parameters)
        {
            var field = new StackPanel { Spacing = 4 };
            field.Children.Add(new TextBlock
            {
                Text = string.Format(CultureInfo.CurrentCulture, ParameterLabelFormat, parameter.Label ?? parameter.Name, parameter.Type, Text(parameter.IsRequired ? "Required" : "Optional")),
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            });
            AddParameterControl(field, parameter);
            if (!string.IsNullOrEmpty(parameter.Description))
            {
                field.Children.Add(new TextBlock { Text = parameter.Description, TextWrapping = TextWrapping.Wrap });
            }

            if (parameter.Default is not null)
            {
                field.Children.Add(new TextBlock
                {
                    Text = string.Format(CultureInfo.CurrentCulture, DefaultFormat, parameter.Default),
                    TextWrapping = TextWrapping.Wrap,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                });
            }

            fields.Children.Add(field);
        }

        _parameterForm = Id(new ScrollViewer { Content = fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, "Parameters");
        ShowParameters();
    }

    private void ShowParameters()
    {
        _status.IsOpen = false;
        _heading.Text = _selected.Name;
        _body.Content = _parameterForm;
        _back.Visibility = Visibility.Visible;
        _refresh.Visibility = Visibility.Collapsed;
        _run.Visibility = Visibility.Visible;
        SetBusy(false);
    }

    private void AddParameterControl(StackPanel field, PowerScriptParameter parameter)
    {
        var id = "Parameter_" + parameter.Name;
        var name = parameter.Label ?? parameter.Name;
        Control control;
        switch (parameter.Type)
        {
            case "bool":
                var check = Id(new CheckBox { Content = name, IsThreeState = true, IsChecked = bool.TryParse(parameter.Default, out var boolean) ? boolean : null }, id);
                _parameterValues.Add(parameter.Name, () => check.IsChecked?.ToString().ToLowerInvariant());
                control = check;
                break;
            case "choice":
                var choice = Id(new ComboBox { ItemsSource = parameter.Options, SelectedItem = parameter.Default, PlaceholderText = Text("NotSet"), HorizontalAlignment = HorizontalAlignment.Stretch }, id);
                _parameterValues.Add(parameter.Name, () => choice.SelectedItem as string);
                control = choice;
                break;
            case "int":
                var number = Id(
                    new NumberBox
                    {
                        Value = int.TryParse(parameter.Default, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer : double.NaN,
                        Minimum = parameter.Min ?? int.MinValue,
                        Maximum = parameter.Max ?? int.MaxValue,
                        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                        PlaceholderText = Text("NotSet"),
                    },
                    id);
                _parameterValues.Add(parameter.Name, () => double.IsNaN(number.Value) ? null : number.Value.ToString(CultureInfo.InvariantCulture));
                control = number;
                break;
            default:
                var text = Id(new TextBox { Text = parameter.Default ?? string.Empty, PlaceholderText = parameter.Type == "file" ? Text("FilePath") : string.Empty }, id);
                _parameterValues.Add(parameter.Name, () => parameter.Type == "file" && text.Text.Length == 0 ? null : text.Text);
                control = text;
                if (parameter.Type == "file")
                {
                    var browse = Button("Browse", "Browse_" + parameter.Name);
                    browse.Click += async (_, _) =>
                    {
                        try
                        {
                            var picker = new FileOpenPicker();
                            picker.FileTypeFilter.Add("*");
                            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                            var file = await picker.PickSingleFileAsync();
                            if (file is not null && !_closed)
                            {
                                text.Text = file.Path;
                            }
                        }
                        catch (Exception ex)
                        {
                            ReportError(ex);
                        }
                    };
                    field.Children.Add(browse);
                }

                break;
        }

        AutomationProperties.SetName(control, name);
        field.Children.Add(control);
    }

    private async Task RunAsync()
    {
        if (_busy || _selected is null)
        {
            return;
        }

        var values = _parameterValues.ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.OrdinalIgnoreCase);
        if (!PowerScriptLauncherModel.TryCollectParameters(_selected.Parameters, values, out var parameters, out var missing))
        {
            ShowMessage(string.Format(CultureInfo.CurrentCulture, MissingParameterFormat, missing), InfoBarSeverity.Error);
            return;
        }

        SetBusy(true);
        _status.IsOpen = false;
        _heading.Text = _selected.Name;
        _refresh.Visibility = Visibility.Collapsed;
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _operation = operation;
        try
        {
            var result = await _client.TransformAsync(
                _selected.Id,
                new PowerScriptTransformInput
                {
                    Parameters = parameters.Count == 0 ? null : parameters.ToDictionary(
                        pair => pair.Key,
                        pair =>
                        {
                            var declaration = _selected.Parameters.Single(parameter => parameter.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                            return declaration.Type switch
                            {
                                "int" => System.Text.Json.JsonSerializer.SerializeToElement(int.Parse(pair.Value, CultureInfo.InvariantCulture)),
                                "bool" => System.Text.Json.JsonSerializer.SerializeToElement(bool.Parse(pair.Value)),
                                _ => System.Text.Json.JsonSerializer.SerializeToElement(pair.Value),
                            };
                        }),
                },
                cancellationToken: operation.Token);
            if (!_closed)
            {
                ShowResult(result);
            }
        }
        catch (OperationCanceledException)
        {
            if (!_closed)
            {
                ShowResult(new PowerScriptTransformResult
                {
                    ExitCode = PowerScriptsProtocol.ExitCode.Timeout,
                    StdErr = Text(_cancelRequested ? "Cancelled" : "RunTimeout"),
                });
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                ShowResult(new PowerScriptTransformResult { ExitCode = PowerScriptsProtocol.ExitCode.Error, StdErr = ex.Message });
                Logger.LogError("PowerScripts launcher execution failed.", ex);
            }
        }
        finally
        {
            _operation = null;
            if (!_closed)
            {
                SetBusy(false);
            }
        }
    }

    private void ShowResult(PowerScriptTransformResult result)
    {
        _catalogueVisible = false;
        _run.Visibility = Visibility.Collapsed;
        _back.Visibility = Visibility.Visible;
        var content = Id(new Grid { RowSpacing = 12 }, "Result");
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(new TextBlock
        {
            Text = result.Succeeded ? Text("Completed") : Text("Failed"),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        FrameworkElement output;
        if (result.Succeeded && _selected.Output == PowerScriptIO.Files && result.FilePaths is { Count: > 0 } paths)
        {
            var files = Id(new ListView { SelectionMode = ListViewSelectionMode.None }, "Files");
            foreach (var path in paths)
            {
                var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, VerticalAlignment = VerticalAlignment.Center });
                var save = Button("SaveAs", "SaveAs_" + files.Items.Count);
                save.Click += async (_, _) => await SaveFileAsync(path, save);
                Grid.SetColumn(save, 1);
                row.Children.Add(save);
                files.Items.Add(row);
            }

            output = files;
            var actions = new StackPanel { Spacing = 8 };
            actions.Children.Add(new TextBlock { Text = Text("FileHelp"), TextWrapping = TextWrapping.Wrap });
            var copy = Button("CopyFiles", "CopyFiles");
            copy.Click += async (_, _) => await CopyFilesAsync(paths, copy);
            actions.Children.Add(copy);
            Grid.SetRow(actions, 2);
            content.Children.Add(actions);
            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                ShowMessage(result.StdErr, InfoBarSeverity.Informational);
            }
        }
        else if (result.Succeeded && (result.Image ?? result.Audio) is { } media)
        {
            var items = Id(new ListView { SelectionMode = ListViewSelectionMode.None }, "Media");
            foreach (var item in media.Items)
            {
                var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock
                {
                    Text = $"{item.MimeType} ({item.Data.Length.ToString("N0", CultureInfo.CurrentCulture)} bytes)",
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                var save = Button("SaveAs", "SaveMedia_" + items.Items.Count);
                save.Click += async (_, _) => await SaveMediaAsync(item, save);
                Grid.SetColumn(save, 1);
                row.Children.Add(save);
                items.Items.Add(row);
            }

            output = items;
            var help = new TextBlock { Text = Text("MediaHelp"), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(help, 2);
            content.Children.Add(help);
            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                ShowMessage(result.StdErr, InfoBarSeverity.Informational);
            }
        }
        else
        {
            var text = result.Succeeded
                ? result.Text ?? Text("NoOutput")
                : string.Format(CultureInfo.CurrentCulture, FailureFormat, result.ExitCode, result.StdErr);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.StdErr))
            {
                text += Environment.NewLine + Environment.NewLine + result.StdErr;
            }

            output = Id(new TextBox { AcceptsReturn = true, Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap }, "ResultText");
            ScrollViewer.SetVerticalScrollBarVisibility(output, ScrollBarVisibility.Auto);
            if (result.Succeeded && result.Text is not null)
            {
                var copy = Button("CopyText", "CopyText");
                copy.Click += (_, _) =>
                {
                    try
                    {
                        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                        package.SetText(result.Text);
                        Clipboard.SetContent(package);
                        Clipboard.Flush();
                        ShowMessage(Text("Copied"), InfoBarSeverity.Success);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                };
                Grid.SetRow(copy, 2);
                content.Children.Add(copy);
            }
        }

        Grid.SetRow(output, 1);
        content.Children.Add(output);
        _body.Content = content;
    }

    private async Task SaveMediaAsync(PowerScriptMediaItem item, Button button)
    {
        button.IsEnabled = false;
        try
        {
            var extension = item.MimeType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                "audio/wav" => ".wav",
                "audio/mpeg" => ".mp3",
                "audio/flac" => ".flac",
                _ => throw new InvalidDataException("Unsupported media MIME type: " + item.MimeType),
            };
            var picker = new FileSavePicker { SuggestedFileName = "powerscript-output" + extension };
            picker.FileTypeChoices.Add(item.MimeType, new List<string> { extension });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var destination = await picker.PickSaveFileAsync();
            if (destination is null || _closed)
            {
                return;
            }

            await FileIO.WriteBytesAsync(destination, item.Data);
            if (!_closed)
            {
                ShowMessage(string.Format(CultureInfo.CurrentCulture, SavedFormat, destination.Path), InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            if (!_closed)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async Task SaveFileAsync(string path, Button button)
    {
        button.IsEnabled = false;
        try
        {
            var source = await StorageFile.GetFileFromPathAsync(path);
            var picker = new FileSavePicker { SuggestedFileName = source.Name };
            var extension = Path.GetExtension(source.Name);
            picker.FileTypeChoices.Add(Text("OutputFiles"), new List<string> { extension.Length > 1 ? extension : "." });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var destination = await picker.PickSaveFileAsync();
            if (destination is null || _closed)
            {
                return;
            }

            if (!source.IsEqual(destination))
            {
                await source.CopyAndReplaceAsync(destination);
            }

            if (!_closed)
            {
                ShowMessage(string.Format(CultureInfo.CurrentCulture, SavedFormat, destination.Path), InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            if (!_closed)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async Task CopyFilesAsync(IReadOnlyList<string> paths, Button button)
    {
        button.IsEnabled = false;
        try
        {
            var files = new List<IStorageItem>();
            foreach (var path in paths)
            {
                files.Add(await StorageFile.GetFileFromPathAsync(path));
            }

            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetStorageItems(files, readOnly: true);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            ShowMessage(Text("Copied"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            if (!_closed)
            {
                button.IsEnabled = true;
            }
        }
    }

    private void CancelOperation()
    {
        _cancelRequested = true;
        _operation?.Cancel();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (busy)
        {
            _cancelRequested = false;
        }

        _body.IsEnabled = !busy;
        _back.IsEnabled = !busy;
        _refresh.IsEnabled = !busy;
        _run.IsEnabled = !busy;
        _progress.IsActive = busy;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        _status.Message = message;
        _status.Severity = severity;
        _status.IsOpen = true;
    }

    private void ReportError(Exception exception)
    {
        Logger.LogError("PowerScripts launcher operation failed.", exception);
        if (!_closed)
        {
            ShowMessage(exception.Message, InfoBarSeverity.Error);
        }
    }
}

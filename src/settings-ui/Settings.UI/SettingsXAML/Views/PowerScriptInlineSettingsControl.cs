// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptInlineSettingsControl : UserControl
{
    private readonly ContentControl _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly InfoBar _status = new() { IsClosable = false };
    private readonly ProgressRing _progress = new() { Width = 24, Height = 24, Visibility = Visibility.Collapsed };
    private readonly Button _save = MakeButton("Save", "Save");
    private readonly Button _discard = MakeButton("Discard", "Discard");
    private readonly Button _retry = MakeButton("Retry", "Retry");
    private PowerScriptsViewModel _viewModel;
    private PowerScriptListItem _script;
    private PowerScriptSettingsForm _form;
    private int _loadedRevision = -1;
    private int _loadGeneration;
    private bool _isBusy;

    public PowerScriptInlineSettingsControl()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 8) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _save.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        buttons.Children.Add(_save);
        buttons.Children.Add(_discard);
        buttons.Children.Add(_progress);
        content.Children.Add(buttons);
        content.Children.Add(new TextBlock
        {
            Text = Text("InlineHelp"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });
        content.Children.Add(_status);
        content.Children.Add(_retry);
        content.Children.Add(_body);
        Content = content;
        AutomationProperties.SetAutomationId(_status, "PowerScriptsInlineStatus");
        _retry.Visibility = Visibility.Collapsed;
        _save.IsEnabled = false;
        _discard.IsEnabled = false;
        _save.Click += Save_Click;
        _discard.Click += Reload_Click;
        _retry.Click += Reload_Click;
        Loaded += Control_Loaded;
        Unloaded += Control_Unloaded;
    }

    public async Task InitializeAsync(PowerScriptsViewModel viewModel, PowerScriptListItem script)
    {
        if (_script is not null)
        {
            return;
        }

        _viewModel = viewModel;
        _script = script;
        _script.PropertyChanged += Script_PropertyChanged;
        await ReloadAsync();
    }

    private static string Text(string key) =>
        ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_Settings" + key);

    private static Button MakeButton(string id, string label)
    {
        var button = new Button { Content = Text(label) };
        AutomationProperties.SetAutomationId(button, "PowerScriptsInline" + id);
        return button;
    }

    private async void Control_Loaded(object sender, RoutedEventArgs args)
    {
        if (_script is not null)
        {
            _script.PropertyChanged -= Script_PropertyChanged;
            _script.PropertyChanged += Script_PropertyChanged;
            if (_loadedRevision != _script.SettingsRevision && !_isBusy)
            {
                await ReloadAsync();
            }
        }
    }

    private void Control_Unloaded(object sender, RoutedEventArgs args)
    {
        if (_script is not null)
        {
            _script.PropertyChanged -= Script_PropertyChanged;
        }
    }

    private async void Script_PropertyChanged(object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PowerScriptListItem.SettingsRevision))
        {
            await ReloadAsync();
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs args) => await ReloadAsync();

    private async Task ReloadAsync()
    {
        var generation = ++_loadGeneration;
        var revision = _script.SettingsRevision;
        SetBusy(true);
        _status.IsOpen = false;
        try
        {
            var snapshot = await _viewModel.GetScriptSettingsAsync(_script.EntryFullPath);
            if (generation == _loadGeneration)
            {
                ShowSnapshot(snapshot);
                _loadedRevision = revision;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
            _retry.Visibility = Visibility.Visible;
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                SetBusy(false);
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs args)
    {
        if (_isBusy || _form is null || !_form.TryGetOverrides(out var overrides))
        {
            return;
        }

        SetBusy(true);
        _status.IsOpen = false;
        try
        {
            var snapshot = await _viewModel.SaveScriptSettingsAsync(_script.EntryFullPath, overrides);
            ShowSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowSnapshot(PowerScriptSettingsSnapshot snapshot)
    {
        _form = new PowerScriptSettingsForm(snapshot, inline: true);
        _body.Content = _form;
        _retry.Visibility = Visibility.Collapsed;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _progress.IsActive = busy;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _body.IsEnabled = !busy;
        _save.IsEnabled = !busy && _form is not null;
        _discard.IsEnabled = !busy && _form is not null;
        _retry.IsEnabled = !busy;
    }

    private void ShowError(Exception exception)
    {
        ManagedCommon.Logger.LogError("Unable to load or save PowerScripts settings.", exception);
        _status.Severity = InfoBarSeverity.Error;
        _status.Message = exception.Message;
        _status.IsOpen = true;
    }
}

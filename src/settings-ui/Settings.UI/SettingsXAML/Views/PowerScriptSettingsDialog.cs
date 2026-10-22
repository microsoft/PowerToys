// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptSettingsDialog : ContentDialog
{
    private readonly PowerScriptSettingsForm _form;
    private readonly Func<JsonObject, Task> _saveAsync;
    private readonly InfoBar _error = new() { IsClosable = false, Severity = InfoBarSeverity.Error };
    private bool _isSaving;

    public PowerScriptSettingsDialog(PowerScriptSettingsSnapshot snapshot, Func<JsonObject, Task> saveAsync)
    {
        _form = new(snapshot);
        _saveAsync = saveAsync;
        var resources = ResourceLoaderInstance.ResourceLoader;
        Title = resources.GetString("PowerScripts_SettingsTitle");
        PrimaryButtonText = resources.GetString("PowerScripts_SettingsSave");
        CloseButtonText = resources.GetString("PowerScripts_SettingsCancel");
        DefaultButton = ContentDialogButton.Close;
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"];
        AutomationProperties.SetAutomationId(this, "PowerScriptsSettingsDialog");

        var content = new StackPanel { Spacing = 16, MinWidth = 480 };
        content.Children.Add(new TextBlock { Text = snapshot.EntryPath, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        content.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational, Message = resources.GetString("PowerScripts_SettingsNotice") });
        content.Children.Add(_error);
        content.Children.Add(_form);
        Content = new ScrollViewer { Content = content, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        PrimaryButtonClick += SaveAsync;
        Closing += (_, args) => args.Cancel = _isSaving;
    }

    private async void SaveAsync(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        args.Cancel = true;
        try
        {
            if (!_form.TryGetOverrides(out var overrides))
            {
                return;
            }

            _isSaving = true;
            ((Control)Content).IsEnabled = false;
            IsPrimaryButtonEnabled = false;
            _error.IsOpen = false;
            await _saveAsync(overrides);
            args.Cancel = false;
        }
        catch (Exception ex)
        {
            _error.Message = ex.Message;
            _error.IsOpen = true;
        }
        finally
        {
            _isSaving = false;
            ((Control)Content).IsEnabled = true;
            IsPrimaryButtonEnabled = true;
            deferral.Complete();
        }
    }
}

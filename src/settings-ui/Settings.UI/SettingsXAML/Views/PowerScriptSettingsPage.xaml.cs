// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptSettingsPage : NavigablePage
{
    private NavigationContext _context;
    private bool _isDialogOpen;
    private bool _isLoading;

    public PowerScriptSettingsPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    public sealed record NavigationContext(
        PowerScriptsViewModel ViewModel,
        PowerScriptListItem Script);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _context = e.Parameter as NavigationContext ??
            throw new ArgumentException("Script settings navigation requires a script and its view model.", nameof(e));
        DataContext = _context.Script;
        _context.Script.PropertyChanged += Script_PropertyChanged;
        UpdateBreadcrumb();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _context.Script.PropertyChanged -= Script_PropertyChanged;
        base.OnNavigatedFrom(e);
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

            var snapshot = await _context.ViewModel.GetScriptSettingsAsync(entryPath);
            MetadataSections.ItemsSource = PowerScriptMetadataDetails.Create(snapshot.Effective, ResourceLoaderInstance.ResourceLoader.GetString);
            MetadataText.Text = snapshot.Effective.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            if (snapshot.ValidationErrors.Length > 0)
            {
                ShowError(new InvalidDataException(string.Join(Environment.NewLine, snapshot.ValidationErrors)));
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
            MetadataCommands.IsEnabled = true;
            _isLoading = false;
        }
    }

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

    private void BackButton_Click(object sender, RoutedEventArgs e) => NavigationService.GoBack();

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = NavigationService.GoBack();

    private void ScriptBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0)
        {
            NavigationService.GoBack();
        }
    }

    private async void ResetScriptButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeScriptRegistrationAsync(isForget: false);

    private async void ForgetScriptButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeScriptRegistrationAsync(isForget: true);

    private async Task ChangeScriptRegistrationAsync(bool isForget)
    {
        if (_isDialogOpen)
        {
            return;
        }

        _isDialogOpen = true;
        try
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString(isForget ? "PowerScripts_SettingsForget" : "PowerScripts_SettingsResetAll"),
                Content = resources.GetString(isForget ? "PowerScripts_SettingsForgetHelp" : "PowerScripts_SettingsResetAllHelp"),
                PrimaryButtonText = resources.GetString(isForget ? "PowerScripts_SettingsForget" : "PowerScripts_SettingsResetAll"),
                CloseButtonText = resources.GetString("PowerScripts_SettingsCancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            if (isForget)
            {
                await _context.ViewModel.ForgetScriptSettingsAsync(_context.Script.EntryFullPath);
                NavigationService.GoBack();
            }
            else
            {
                await _context.ViewModel.ResetScriptSettingsAsync(_context.Script.EntryFullPath);
            }

            ScriptSettingsError.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private async void ConfigureScriptMxcPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDialogOpen)
        {
            return;
        }

        _isDialogOpen = true;
        try
        {
            var dialog = new PowerScriptMxcPolicyDialog(_context.Script) { XamlRoot = XamlRoot };
            dialog.SaveAsync = async () =>
            {
                if (!await _context.ViewModel.TrySetScriptMxcConfigurationAsync(
                    _context.Script,
                    dialog.MxcModeIndex,
                    dialog.EnabledPolicies,
                    dialog.UsesGlobalPolicy,
                    dialog.RiskAccepted))
                {
                    throw new InvalidOperationException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_SettingsHostFailed"));
                }
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _isDialogOpen = false;
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

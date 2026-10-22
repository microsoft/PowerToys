// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerScriptsPage : NavigablePage, IRefreshablePage
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly SettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly Dictionary<string, ScriptEditorView> _scriptEditors = new(StringComparer.OrdinalIgnoreCase);
        private bool _isScriptDialogOpen;

        private PowerScriptsViewModel ViewModel { get; set; }

        public PowerScriptsPage()
        {
            _settingsUtils = SettingsUtils.Default;
            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);

            ViewModel = new PowerScriptsViewModel(_generalSettingsRepository, ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;

            InitializeComponent();
            Loaded += PowerScriptsPage_Loaded;
        }

        public async void RefreshEnabledState()
        {
            await RefreshScriptsAsync();
        }

        public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

        private void ScriptIcon_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PowerScriptListItem script })
            {
                Logger.LogWarning($"Unable to display PowerScript icon '{script.IconPath}': {e.ErrorMessage}");
                script.MarkIconUnavailable();
            }
        }

        private async void PowerScriptsPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= PowerScriptsPage_Loaded;
            await RefreshScriptsAsync();
            await ViewModel.RefreshMxcPlatformSupportAsync();
        }

        private async void BrowseScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = await PickSingleFolderDialog();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ViewModel.SetScriptsFolder(folder);
                await RefreshScriptsAsync();
            }
        }

        private async void ResetScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetScriptsFolder();
            await RefreshScriptsAsync();
        }

        private async Task RefreshScriptsAsync()
        {
            try
            {
                await ViewModel.ReloadScriptsAsync();
                PruneScriptEditors();
                ScriptSettingsError.IsOpen = false;
            }
            catch (Exception ex)
            {
                ShowScriptSettingsError(ex);
            }
        }

        private async void AddScriptButton_Click(object sender, RoutedEventArgs e)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var path = PickFileDialog(
                resources.GetString("PowerScripts_SettingsFileFilter") + "\0*.ps1;*.py\0",
                resources.GetString("PowerScripts_SettingsAdd"));
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!File.Exists(path) ||
                    (!string.Equals(Path.GetExtension(path), ".ps1", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(Path.GetExtension(path), ".py", StringComparison.OrdinalIgnoreCase)))
                {
                    ShowScriptSettingsError(new InvalidOperationException(resources.GetString("PowerScripts_SettingsChooseExistingScript")));
                    return;
                }

                await ConfigureScriptAsync(path);
            }
        }

        private async void InlineScriptSettings_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ContentControl { DataContext: PowerScriptListItem script } host)
            {
                var key = GetScriptEditorKey(script);
                if (!_scriptEditors.TryGetValue(key, out var view))
                {
                    view = new ScriptEditorView();
                    _scriptEditors.Add(key, view);
                }

                // Viewport unloads must retain drafts; detach only when a replacement host is ready.
                if (view.Host is not null && !ReferenceEquals(view.Host, host))
                {
                    view.Host.Content = null;
                }

                view.Host = host;
                if (!ReferenceEquals(host.Content, view.Editor))
                {
                    host.Content = view.Editor;
                }

                await view.Editor.InitializeAsync(ViewModel, script);
            }
        }

        private string GetScriptEditorKey(PowerScriptListItem script) => ViewModel.ScriptsFolder + "\0" + script.EntryFullPath;

        private void PruneScriptEditors()
        {
            var retained = ViewModel.Scripts.Select(GetScriptEditorKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _scriptEditors.Keys.Where(key => !retained.Contains(key)).ToArray())
            {
                _scriptEditors.Remove(key);
            }
        }

        private async Task ConfigureScriptAsync(string entryPath)
        {
            if (_isScriptDialogOpen)
            {
                return;
            }

            _isScriptDialogOpen = true;
            try
            {
                var snapshot = await ViewModel.GetScriptSettingsAsync(entryPath);
                ScriptSettingsError.IsOpen = false;
                var dialog = new PowerScriptSettingsDialog(
                    snapshot,
                    overrides => ViewModel.SaveScriptSettingsAsync(entryPath, overrides))
                {
                    XamlRoot = XamlRoot,
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ShowScriptSettingsError(ex);
            }
            finally
            {
                _isScriptDialogOpen = false;
            }
        }

        private async void ResetScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PowerScriptListItem script })
            {
                await ChangeScriptRegistrationAsync(script, isForget: false);
            }
        }

        private async void ForgetScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PowerScriptListItem script })
            {
                await ChangeScriptRegistrationAsync(script, isForget: true);
            }
        }

        private async Task ChangeScriptRegistrationAsync(PowerScriptListItem script, bool isForget)
        {
            if (_isScriptDialogOpen)
            {
                return;
            }

            _isScriptDialogOpen = true;
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
                    await ViewModel.ForgetScriptSettingsAsync(script.EntryFullPath);
                }
                else
                {
                    await ViewModel.ResetScriptSettingsAsync(script.EntryFullPath);
                }

                PruneScriptEditors();
                ScriptSettingsError.IsOpen = false;
            }
            catch (Exception ex)
            {
                ShowScriptSettingsError(ex);
            }
            finally
            {
                _isScriptDialogOpen = false;
            }
        }

        private void ShowScriptSettingsError(Exception exception)
        {
            ScriptSettingsError.Message = exception.Message;
            ScriptSettingsError.IsOpen = true;
        }

        private sealed class ScriptEditorView
        {
            public PowerScriptInlineSettingsControl Editor { get; } = new();

            public ContentControl Host { get; set; }
        }

        // Opens the current scripts root folder in File Explorer.
        private void OpenScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = ViewModel.ScriptsFolder;
            try
            {
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                    Process.Start("explorer.exe", $"\"{folder}\"");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open PowerScripts folder '{folder}'.", ex);
            }
        }

        // Opens the script's folder in File Explorer, selecting the entry file when known.
        private void OpenScriptFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: PowerScriptListItem script })
            {
                return;
            }

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
                Logger.LogError($"Failed to open PowerScript folder '{script.FolderPath}'.", ex);
            }
        }

        private void BrowsePythonInterpreterButton_Click(object sender, RoutedEventArgs e)
        {
            var file = PickFileDialog(
                "Python Executable\0python.exe;python3.exe;py.exe\0All Executables\0*.exe\0",
                "Select Python interpreter");
            if (!string.IsNullOrWhiteSpace(file))
            {
                ViewModel.SetPythonInterpreterPath(file);
            }
        }

        private async void MxcEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle || toggle.IsOn == ViewModel.IsMxcEnabled)
            {
                return;
            }

            var isConfirmed = toggle.IsOn ||
                              await ShowMxcWeakeningWarningAsync(
                                  ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_MxcDisableWarning"));
            if (!ViewModel.TrySetMxcEnabled(toggle.IsOn, isConfirmed))
            {
                toggle.IsOn = ViewModel.IsMxcEnabled;
            }
        }

        private async void GlobalMxcPolicyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch { Tag: string policy } toggle ||
                toggle.IsOn == ViewModel.IsGlobalMxcPolicyEnabled(policy))
            {
                return;
            }

            var isConfirmed = toggle.IsOn ||
                              await ShowMxcWeakeningWarningAsync(
                                  ResourceLoaderInstance.ResourceLoader
                                      .GetString("PowerScripts_MxcDisablePolicyWarning")
                                      .Replace(
                                          "{0}",
                                          PowerScriptListItem.GetMxcPolicyDisplayName(policy),
                                          StringComparison.Ordinal));
            if (!ViewModel.TrySetGlobalMxcPolicy(policy, toggle.IsOn, isConfirmed))
            {
                toggle.IsOn = ViewModel.IsGlobalMxcPolicyEnabled(policy);
            }
        }

        private async void ConfigureScriptMxcPolicyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isScriptDialogOpen || sender is not FrameworkElement { DataContext: PowerScriptListItem script })
            {
                return;
            }

            _isScriptDialogOpen = true;
            try
            {
                var dialog = new PowerScriptMxcPolicyDialog(script)
                {
                    XamlRoot = XamlRoot,
                };
                dialog.SaveAsync = async () =>
                {
                    if (!await ViewModel.TrySetScriptMxcConfigurationAsync(
                        script,
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
                ShowScriptSettingsError(ex);
            }
            finally
            {
                _isScriptDialogOpen = false;
            }
        }

        private async Task<bool> ShowMxcWeakeningWarningAsync(string changeDescription)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resourceLoader.GetString("PowerScripts_MxcRiskDialogTitle"),
                Content = resourceLoader.GetString("PowerScripts_MxcRiskDialogContent")
                    .Replace("{0}", changeDescription, StringComparison.Ordinal),
                PrimaryButtonText = resourceLoader.GetString("PowerScripts_MxcRiskDialogAccept"),
                CloseButtonText = resourceLoader.GetString("PowerScripts_MxcPolicyDialog_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        // Uses the Win32 OpenFileName dialog as FileOpenPicker doesn't work when Settings runs elevated.
        private static string PickFileDialog(string filter, string title)
        {
            OpenFileName openFileName = new OpenFileName();
            openFileName.StructSize = Marshal.SizeOf(openFileName);
            openFileName.Filter = filter;

            // Make buffer double MAX_PATH since it can use 2 chars per char.
            openFileName.File = new string(new char[260 * 2]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[260 * 2]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.Title = title;
            openFileName.DefExt = null;
            openFileName.Flags = (int)OpenFileNameFlags.OFN_NOCHANGEDIR;
            openFileName.Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());

            return NativeMethods.GetOpenFileName(openFileName) ? openFileName.File : null;
        }

        private async Task<string> PickSingleFolderDialog()
        {
            // Use the shell32 folder dialog (works even when Settings runs elevated), matching GeneralPage.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            return await Task.FromResult(ShellGetFolder.GetFolderDialog(hwnd));
        }
    }
}

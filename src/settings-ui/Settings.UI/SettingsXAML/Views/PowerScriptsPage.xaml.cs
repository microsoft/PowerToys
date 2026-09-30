// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
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

        public void RefreshEnabledState()
        {
            ViewModel.ReloadScripts();
        }

        private async void PowerScriptsPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= PowerScriptsPage_Loaded;
            await ViewModel.RefreshMxcPlatformSupportAsync();
        }

        private async void BrowseScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = await PickSingleFolderDialog();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ViewModel.SetScriptsFolder(folder);
            }
        }

        private void ResetScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetScriptsFolder();
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

        private async void BrowseMxcExecutorButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var file = PickFileDialog(
                "MXC Executor\0wxc-exec.exe\0All Executables\0*.exe\0",
                resourceLoader.GetString("PowerScripts_MxcExecutorPickerTitle"));
            if (!string.IsNullOrWhiteSpace(file))
            {
                ViewModel.SetMxcExecutorPath(file);
                await ViewModel.RefreshMxcPlatformSupportAsync();
            }
        }

        private async void MxcExecutorPathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ViewModel.MxcExecutorPath = textBox.Text;
                await ViewModel.RefreshMxcPlatformSupportAsync();
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
            if (sender is not FrameworkElement { DataContext: PowerScriptListItem script })
            {
                return;
            }

            var dialog = new PowerScriptMxcPolicyDialog(script)
            {
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ViewModel.TrySetScriptMxcConfiguration(
                script,
                dialog.MxcModeIndex,
                dialog.EnabledPolicies,
                dialog.UsesGlobalPolicy,
                dialog.RiskAccepted);
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

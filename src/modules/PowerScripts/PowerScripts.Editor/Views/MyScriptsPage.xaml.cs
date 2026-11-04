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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.Helpers;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views
{
    public sealed partial class MyScriptsPage : Page
    {
        private bool _preserveCatalogueOnLoad;

        private PowerScriptsViewModel ViewModel { get; set; }

        public MyScriptsPage()
        {
            NavigationCacheMode = NavigationCacheMode.Required;
            InitializeComponent();
            Loaded += MyScriptsPage_Loaded;
        }

        public async void RefreshEnabledState()
        {
            await RefreshConsumersAsync();
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

        private async void MyScriptsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshConsumersAsync();
            if (_preserveCatalogueOnLoad)
            {
                return;
            }

            await RefreshScriptsAsync();
            await ViewModel.RefreshMxcPlatformSupportAsync();
            if (ViewModel.IsManagedLinuxMode)
            {
                await ViewModel.RefreshLinuxContainerAsync();
            }
        }

        private async Task RefreshConsumersAsync()
        {
            try
            {
                await ViewModel.RefreshConsumersAsync();
            }
            catch (Exception exception)
            {
                ShowScriptSettingsError(exception);
            }
        }

        private async void PowerScriptsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle && ViewModel.CanChangePowerScriptsEnabled && toggle.IsOn != ViewModel.IsEnabled)
            {
                try
                {
                    ViewModel.IsEnabled = toggle.IsOn;
                    await ViewModel.RefreshConsumersAsync();
                }
                catch (Exception exception)
                {
                    ShowScriptSettingsError(exception);
                }
            }
        }

        private async void PythonEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedIndex: 2 } && ViewModel.IsMxcEnabled)
            {
                await ViewModel.RefreshLinuxContainerAsync();
            }
        }

        private async void CheckLinuxContainer_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.RefreshLinuxContainerAsync();

        private async void RecoverLinuxContainer_Click(object sender, RoutedEventArgs e)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("PowerScripts_LinuxRecoverTitle"),
                Content = resources.GetString("PowerScripts_LinuxRecoverHelp"),
                PrimaryButtonText = resources.GetString("PowerScripts_LinuxRecoverConfirm"),
                CloseButtonText = resources.GetString("PowerScripts_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.RefreshLinuxContainerAsync(recover: true);
            }
        }

        private async void SetupLinuxContainer_Click(object sender, RoutedEventArgs e)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("PowerScripts_LinuxSetupTitle"),
                Content = resources.GetString("PowerScripts_LinuxSetupHelp")
                    .Replace("{0}", ViewModel.LinuxContainerImage, StringComparison.Ordinal),
                PrimaryButtonText = resources.GetString("PowerScripts_LinuxSetupConfirm"),
                CloseButtonText = resources.GetString("PowerScripts_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.RefreshLinuxContainerAsync(prepare: true);
            }
        }

        private void CreateScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Frame.Navigate(typeof(PowerScriptTemplatesPage), ViewModel))
            {
                ShowScriptSettingsError(new InvalidOperationException(
                    ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateNavigationFailed")));
            }
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
                ScriptSettingsError.IsOpen = false;
            }
            catch (Exception ex)
            {
                ShowScriptSettingsError(ex);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel = e.Parameter as PowerScriptsViewModel ??
                throw new ArgumentException("Script navigation requires the PowerScripts view model.", nameof(e));
            DataContext = ViewModel;
            Bindings.Update();
            _preserveCatalogueOnLoad = e.NavigationMode is NavigationMode.Back or NavigationMode.Forward;
        }

        private void ScriptSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PowerScriptListItem script })
            {
                if (!Frame.Navigate(
                    typeof(PowerScriptSettingsPage),
                    new PowerScriptSettingsPage.NavigationContext(ViewModel, script)))
                {
                    ShowScriptSettingsError(new InvalidOperationException(
                        ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_SettingsNavigationFailed")));
                }
            }
        }

        private void ShowScriptSettingsError(Exception exception)
        {
            ScriptSettingsError.Message = exception.Message;
            ScriptSettingsError.IsOpen = true;
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
            else if (ViewModel.IsManagedLinuxMode)
            {
                await ViewModel.RefreshLinuxContainerAsync();
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
                CloseButtonText = resourceLoader.GetString("PowerScripts_Cancel"),
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
            openFileName.Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(PowerScripts.Editor.App.MainWindow);

            return NativeMethods.GetOpenFileName(openFileName) ? openFileName.File : null;
        }

        private async Task<string> PickSingleFolderDialog()
        {
            // Use the shell32 folder dialog (works even when Settings runs elevated), matching GeneralPage.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(PowerScripts.Editor.App.MainWindow);
            return await Task.FromResult(ShellGetFolder.GetFolderDialog(hwnd));
        }
    }
}

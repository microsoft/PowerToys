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
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerScriptsPage : NavigablePage, IRefreshablePage
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly SettingsRepository<GeneralSettings> _generalSettingsRepository;
        private bool _preserveCatalogueOnLoad;

        private PowerScriptsViewModel ViewModel { get; set; }

        public PowerScriptsPage()
        {
            NavigationCacheMode = NavigationCacheMode.Required;
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

        public Visibility TestingLauncherVisibility =>
#if DEBUG
            Visibility.Visible;
#else
            Visibility.Collapsed;
#endif

        private void OpenLauncherButton_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            App.OpenSettingsWindow(typeof(PowerScriptLauncherWindow));
#endif
        }

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

        private async void PythonEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedIndex: 2 } && ViewModel.IsMxcEnabled)
            {
                await ViewModel.RefreshLinuxContainerAsync();
            }
        }

        private async void CheckLinuxContainer_Click(object sender, RoutedEventArgs e) =>
            await ViewModel.RefreshLinuxContainerAsync();

        private async void SetupLinuxContainer_Click(object sender, RoutedEventArgs e)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("PowerScripts_LinuxSetupTitle"),
                Content = resources.GetString("PowerScripts_LinuxSetupHelp"),
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
            if (!NavigationService.Navigate<PowerScriptTemplatesPage>(ViewModel))
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
            _preserveCatalogueOnLoad = e.NavigationMode is NavigationMode.Back or NavigationMode.Forward;
        }

        private void ScriptSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: PowerScriptListItem script })
            {
                if (!NavigationService.Navigate<PowerScriptSettingsPage>(
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

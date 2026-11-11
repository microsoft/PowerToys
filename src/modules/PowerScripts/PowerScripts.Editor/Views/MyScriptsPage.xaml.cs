// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
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
        private bool _refreshingConfiguration;

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

        public static bool IsDisabled(bool value) => !value;

        public async Task RefreshConfigurationAsync()
        {
            if (ViewModel is null || _refreshingConfiguration)
            {
                return;
            }

            _refreshingConfiguration = true;
            try
            {
                ViewModel.RefreshConfiguration();
                await ViewModel.RefreshConsumersAsync();
                await ViewModel.RefreshMxcPlatformSupportAsync();
                if (ViewModel.IsManagedLinuxMode)
                {
                    await ViewModel.RefreshLinuxContainerAsync();
                }

                await RefreshScriptsAsync();
            }
            catch (Exception exception)
            {
                ShowScriptSettingsError(exception);
            }
            finally
            {
                _refreshingConfiguration = false;
            }
        }

        private void OpenGlobalSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var runner = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.exe"));
                using var process = Process.Start(new ProcessStartInfo(runner, "--open-settings=PowerScripts") { UseShellExecute = false })
                    ?? throw new InvalidOperationException("Unable to open PowerToys Settings.");
            }
            catch (Exception exception)
            {
                ShowScriptSettingsError(exception);
            }
        }

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
            if (_preserveCatalogueOnLoad)
            {
                return;
            }

            await RefreshConfigurationAsync();
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

        private void CreateCustomScript_Click(object sender, RoutedEventArgs e)
        {
            if (!Frame.Navigate(typeof(PowerScriptTemplatesPage), ViewModel))
            {
                ShowScriptSettingsError(new InvalidOperationException(
                    ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateNavigationFailed")));
            }
        }

        private void CreatePowerToysOperations_Click(object sender, RoutedEventArgs e)
        {
            if (!Frame.Navigate(typeof(PowerToysOperationsPage), ViewModel))
            {
                ShowScriptSettingsError(new InvalidOperationException(
                    ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_OperationNavigationFailed")));
            }
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
            Logger.LogError("Unable to manage PowerScripts.", exception);
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
                ShowScriptSettingsError(ex);
            }
        }
    }
}

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
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.Helpers;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views
{
    public sealed partial class MyScriptsPage : Page
    {
        private bool _preserveCatalogueOnLoad;
        private bool _refreshingConfiguration;
        private string _newScriptChoice;
        private bool _isShowingNewScriptDialog;

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

        public static Visibility FilterBadgeVisibility(int inputFilterIndex, int outputFilterIndex) =>
            BoolToVisibility(inputFilterIndex > 0 || outputFilterIndex > 0);

        public static Visibility ListVisibility(bool hasScripts, bool hasNoMatchingScripts) =>
            BoolToVisibility(hasScripts && !hasNoMatchingScripts);

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
                await ViewModel.RefreshActivityAsync();
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

        private static PowerScriptListItem ScriptFromMenu(object sender) => (PowerScriptListItem)((FrameworkElement)sender).Tag;

        private static void SetMoreButtonOpacity(object row, double opacity)
        {
            if (row is Grid { Children.Count: > 0 } outer
                && outer.Children[0] is Grid inner
                && inner.Children[^1] is Button more)
            {
                more.Opacity = opacity;
            }
        }

        private void ScriptRow_PointerEntered(object sender, PointerRoutedEventArgs e) => SetMoreButtonOpacity(sender, 1);

        private void ScriptRow_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid { Children.Count: > 0 } outer
                && outer.Children[0] is Grid inner
                && inner.Children[^1] is Button { Flyout.IsOpen: true })
            {
                return;
            }

            SetMoreButtonOpacity(sender, 0);
        }

        private void ScriptMoreButton_GotFocus(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.Opacity = 1;
            button.LostFocus -= ScriptMoreButton_LostFocus;
            button.LostFocus += ScriptMoreButton_LostFocus;
        }

        private void ScriptMoreButton_LostFocus(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (button.Flyout is not { IsOpen: true })
            {
                button.Opacity = 0;
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

        private async void CreateScript_Click(object sender, RoutedEventArgs e)
        {
            if (_isShowingNewScriptDialog)
            {
                return;
            }

            _isShowingNewScriptDialog = true;
            CreateScriptButton.IsEnabled = false;
            try
            {
                _newScriptChoice = null;
                NewScriptDialog.XamlRoot = XamlRoot;
                await NewScriptDialog.ShowAsync();

                var (page, failure) = _newScriptChoice switch
                {
                    "Script" => (typeof(PowerScriptTemplatesPage), "PowerScripts_TemplateNavigationFailed"),
                    "Operation" => (typeof(PowerToysOperationsPage), "PowerScripts_OperationNavigationFailed"),
                    _ => (null, null),
                };

                if (page is not null && !Frame.Navigate(page, ViewModel))
                {
                    ShowScriptSettingsError(new InvalidOperationException(
                        ResourceLoaderInstance.ResourceLoader.GetString(failure)));
                }
            }
            catch (Exception exception)
            {
                ShowScriptSettingsError(exception);
            }
            finally
            {
                _isShowingNewScriptDialog = false;
                CreateScriptButton.IsEnabled = ViewModel.IsEnabled;
            }
        }

        private void NewScriptChoice_Click(object sender, RoutedEventArgs e)
        {
            _newScriptChoice = (string)((FrameworkElement)sender).Tag;
            NewScriptDialog.Hide();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ScriptInputFilterIndex = 0;
            ViewModel.ScriptOutputFilterIndex = 0;
            ViewModel.ScriptSearchText = string.Empty;
        }

        private async Task RefreshScriptsAsync()
        {
            try
            {
                await ViewModel.ReloadScriptsAsync();
                ScriptSettingsError.IsOpen = false;
                ScriptSettingsError.Visibility = Visibility.Collapsed;
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

        private void ScriptList_ItemClick(object sender, ItemClickEventArgs e) => OpenScript((PowerScriptListItem)e.ClickedItem);

        private void OpenScriptMenuItem_Click(object sender, RoutedEventArgs e) => OpenScript(ScriptFromMenu(sender));

        private void EditScriptMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PowerScriptFileEditor.Open(ScriptFromMenu(sender).EntryFullPath, isMetadata: false);
            }
            catch (Exception ex)
            {
                ShowScriptSettingsError(ex);
            }
        }

        private void ShowInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var script = ScriptFromMenu(sender);
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
                ShowScriptSettingsError(ex);
            }
        }

        private void ScriptIcon_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PowerScriptListItem script)
            {
                Logger.LogWarning($"Unable to display PowerScript icon '{script.IconPath}': {e.ErrorMessage}");
                script.MarkIconUnavailable();
            }
        }

        private void OpenScript(PowerScriptListItem script)
        {
            if (!Frame.Navigate(
                    typeof(PowerScriptSettingsPage),
                    new PowerScriptSettingsPage.NavigationContext(ViewModel, script)))
            {
                ShowScriptSettingsError(new InvalidOperationException(
                    ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_SettingsNavigationFailed")));
            }
        }

        private void ShowScriptSettingsError(Exception exception)
        {
            Logger.LogError("Unable to manage PowerScripts.", exception);
            ScriptSettingsError.Message = exception.Message;
            ScriptSettingsError.Visibility = Visibility.Visible;
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
